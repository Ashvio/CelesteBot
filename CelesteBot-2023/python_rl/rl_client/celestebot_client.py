#!/usr/bin/env python
"""
Example of running an external simulator (a simple CartPole env
in this case) against an RLlib policy server listening on one or more
HTTP-speaking port(s). See `cartpole_server.py` in this same directory for
how to start this server.

This script will only create one single env altogether to illustrate
that RLlib can run w/o needing an internalized environment.

Setup:
1) Start the policy server:
    See `cartpole_server.py` on how to do this.
2) Run this client:
    $ python cartpole_client.py --inference-mode=local|remote --[other options]
      Use --help for help.

In "local" inference-mode, the action computations are performed
inside the PolicyClient used in this script w/o sending an HTTP request
to the server. This reduces network communication overhead, but requires
the PolicyClient to create its own RolloutWorker (+Policy) based on
the server's config. The PolicyClient will retrieve this config automatically.
You do not need to define the RLlib config dict here!

In "remote" inference mode, the PolicyClient will send action requests to the
server and not compute its own actions locally. The server then performs the
inference forward pass and returns the action to the client.

In either case, the user of PolicyClient must:
- Declare new episodes and finished episodes to the PolicyClient.
- Log rewards to the PolicyClient.
- Call `get_action` to receive an action from the PolicyClient (whether it'd be
  computed locally or remotely).
- Besides `get_action`, the user may let the PolicyClient know about
  off-policy actions having been taken via `log_action`. This can be used in
  combination with `get_action`, but will only work, if the connected server
  runs an off-policy RL algorithm (such as DQN, SAC, or DDPG).
"""

import argparse
import logging
import math
import os
import pickle
import queue
import re
import threading
import time
import traceback
from collections import OrderedDict
from threading import Thread
from typing import List, SupportsFloat, Optional
import socket, errno
from urllib.error import HTTPError

import numpy as np
import requests
from ray.rllib.env.policy_client import PolicyClient

from python_rl.rl_common.celestebot_env import CelesteEnv, TerminationEvent
from python_rl.rl_server import celestebot_server

parser = argparse.ArgumentParser()
parser.add_argument(
    "--no-train", action="store_true", help="Whether to disable training."
)
parser.add_argument(
    "--inference-mode", type=str, default="local", choices=["local", "remote"]
)
parser.add_argument(
    "--off-policy",
    action="store_true",
    help="Whether to compute random actions instead of on-policy "
         "(Policy-computed) ones.",
)
parser.add_argument(
    "--stop-reward",
    type=float,
    default=9999,
    help="Stop once the specified reward is reached.",
)
parser.add_argument(
    "--port", type=int, default=9900, help="The port to use (on localhost)."
)


def _get_available_port(base_port: int = 9900) -> int:
    current_port = base_port
    while current_port < base_port + 100:

        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
            try:
                s.bind(("127.0.0.1", 5555))
            except socket.error as e:
                if e.errno == errno.EADDRINUSE:
                    current_port += 1
                    continue
                else:
                    return current_port
            current_port += 1


class CelesteClient:

    def __init__(self, action_queue=None):
        # The following line is the only instance, where an actual env will
        # be created in this entire example (including the server side!).
        # This is to demonstrate that RLlib does not require you to create
        # unnecessary env objects within the PolicyClient/Server objects, but
        # that only this following env and the loop below runs the entire
        # training process.
        self.episode_rewards = 0
        self.awaiting_rewards = 0
        # If server has n workers, all ports between 9900 and 990[n-1] should
        # be listened on. E.g. if server has num_workers=2, try 9900 or 9901.
        # Note that no config is needed in this script as it will be defined
        # on and sent from the server.
        self.python_logs_txt = "python_logs.txt"
        logging.basicConfig(filename=self.python_logs_txt,
                            filemode='w+',
                            datefmt='%H:%M:%S',
                            level=logging.INFO,
                            )
        self.logger = logging.getLogger('PythonClientLogger')
        self.logger.log(logging.INFO, "Python client started")
        session = requests.Session()
        # TODO: Get worker number based on path of executable. Each worker will run in a different copy of Celeste,
        #  eg /Celeste_001, /Celeste_002
        local_path = os.getcwd()
        # extract worker number from path from regex "Celeste_(\d+)"
        m = re.search(".+Celeste_([0-9]+)", local_path)
        if m:
            self.is_worker = True
            worker_number = int(m.group(1))
        else:
            # base Celeste game
            self.is_worker = False
            worker_number = 0
        num_client_workers = int(os.environ.get("NUM_CLIENT_WORKERS", 4))
        num_server_workers = celestebot_server.NUM_WORKERS
        num_clients_per_server = max(num_client_workers // num_server_workers, 1)
        server_number = worker_number // num_clients_per_server
        self.logger.log(logging.INFO, f"Worker number: {worker_number}")
        self.logger.log(logging.INFO, f"Num client workers: {num_client_workers}")
        self.logger.log(logging.INFO, f"Num server workers: {num_server_workers}")
        self.logger.log(logging.INFO, f"Num clients per server: {num_clients_per_server}")
        self.logger.log(logging.INFO, f"Assigned Server number: {server_number}")
        if server_number >= num_server_workers:
            server_number -= 1
        port = 9900 + server_number
        self.logger.log(logging.INFO, f"Connecting to port {port}")
        # port = 9900
        self.client = PolicyClient(
            f"http://127.0.0.1:{port}", inference_mode="remote", session=session
        )
        self.current_episode_id = ""
        self._first_reward = True
        if self.is_worker:
            log_level = logging.INFO
        else:
            log_level = logging.DEBUG
        self.logger.setLevel(log_level)
        logging.getLogger('requests').setLevel(logging.CRITICAL)
        # TODO: Figure out why connections keep closing (HTTP BaseHandler is 1.0 not 1.1)
        logging.getLogger('urllib3.connectionpool').setLevel(logging.CRITICAL)
        logging.getLogger('urllib3').setLevel(logging.CRITICAL)
        # self.observation_processor = Thread(target=self.process_observation_queue)
        # self.observation_processor.start()

        self.info_queue = queue.Queue()
        self.env = CelesteEnv(action_queue, logger=self.logger)

    # def ext_test(self):
    #     print("Hello from python")
    #     while True:
    #         self.env.add_action(np.array([0, 1, 1, 0]))
    #         time.sleep(30)

    def ext_add_observation(self, vision, speed_x_y, can_dash, stamina, death_flag, finished_level, target, position,
                            screen_position, is_climbing, on_ground):
        # send observation from .NET to server and get the action and send it to the queue
        observation = OrderedDict()
        observation["can_dash"] = np.array([can_dash])
        observation["is_climbing"] = np.array([is_climbing])

        observation["map_entities_vision"] = np.array(vision)
        observation["on_ground"] = np.array([on_ground])
        observation["position"] = np.array(position)
        # observation["screen_position"] = np.array(screen_position)

        observation["speed_x_y"] = np.array(speed_x_y)
        observation["stamina"] = np.array([stamina])
        observation["target"] = np.array(target)  # array of x,y coord

        self.env.observation_queue.put(observation)

        if death_flag:
            self.env.termination_event_queue.put(TerminationEvent.DEATH)
        elif finished_level:
            self.env.termination_event_queue.put(TerminationEvent.FINISHED_LEVEL)
        else:
            self.env.termination_event_queue.put(TerminationEvent.NORMAL)

    def ext_add_reward(self, reward):
        self.env.reward_queue.put(reward)

    def ext_get_action(self):
        # send action to .NET
        # time how long this takes:
        nest_obs = self.env.observation_queue.get()
        action = self.client.get_action(self.current_episode_id, nest_obs)
        return [int(x) for x in action.tolist()]

    def log_rewards(self):
        while True:
            try:
                reward = self.env.reward_queue.get(timeout=0.5)
            except queue.Empty:
                self.logger.log(logging.INFO, "Timed out waiting for reward")
                self.logger.log(logging.INFO, "queue size: " + str(self.env.reward_queue.qsize()))
                reward = 0.0
            info = self.info_queue.get()
            self.client.log_returns(self.current_episode_id, reward, info=info)
            self.episode_rewards += reward
            self.info_queue.task_done()

    def start_training(self):
        # In the following, we will use our external environment (the CartPole
        # env we created above) in connection with the PolicyClient to query
        # actions (from the server if "remote"; if "local" we'll compute them
        # on this client side), and send back observations and rewards.
        try:

            # Start a new episode.
            obs, info = self.env.reset()
            self.current_episode_id = self.client.start_episode(training_enabled=True)
            self.logger.log(logging.INFO, "Started episode, observation: " + str(obs))
            start_time = time.time()
            action_count = 0
            reward_logger = threading.Thread(target=self.log_rewards)
            reward_logger.start()
            while True:
                # Compute an action randomly (off-policy) and log it.

                # Compute an action locally or remotely (on server).
                # No need to log it here as the action
                # self.logger.log(logging.DEBUG, "Querying action: " + str(episode_id))
                action_count += 1
                try:
                    action = self.client.get_action(self.current_episode_id, obs)
                except HTTPError as e:
                    self.logger.log(logging.ERROR, f"HTTP Error when processing observation: {obs}")
                    self.logger.log(logging.ERROR, f"HTTP Error: {e.reason}")
                    self.logger.log(logging.ERROR, f"HTTP Error: {e.headers}")
                    raise e
                # self.logger.log(logging.DEBUG, "Got action: " + str(action))
                # Perform a step in the external simulator (env).

                obs, reward, terminated, truncated, info = self.env.step(action)
                self.awaiting_rewards += 1
                if action_count % 1 == 0:
                    self.logger.log(logging.DEBUG, f"Reward for Action {action}: {reward}")
                    # self.logger.log(logging.DEBUG, f"Observation: {obs}")

                self.client.log_returns(self.current_episode_id, np.float64(reward))
                self.episode_rewards += reward

                # Log next-obs, rewards, and infos.
                # self.info_queue.put(info)
                # self.log_reward()
                # self.info_queue.join()
                # self.client.log_returns(episode_id, reward, info=info)
                # Reset the episode if done.
                if terminated or truncated:
                    # wait for all rewards to have been sent
                    self.logger.log(logging.INFO,
                                    f"Total reward for episode: {self.episode_rewards}. Episode ended due to: {info}")
                    end_time = time.time()
                    self.logger.log(logging.INFO,
                                    f"Episode took {end_time - start_time} seconds and  {action_count / (end_time - start_time)} actions per second")
                    # self.info_queue.join()

                    start_time = time.time()
                    action_count = 0
                    self.episode_rewards = 0.0

                    # End the old episode.
                    self.client.end_episode(self.current_episode_id, obs)
                    # Tell Madeline to do nothing to get the next observation
                    # Start a new episode.
                    obs, info = self.env.reset()
                    self.current_episode_id = self.client.start_episode(training_enabled=True)
        except Exception as e:
            with open(self.python_logs_txt, 'a') as f:
                f.write(str(e))
                f.write(traceback.format_exc())


from enum import Enum


class UpDownActionType(Enum):
    NOOP = 0
    Up = 1
    Down = 2


class LeftRightActionType(Enum):
    NOOP = 0
    Left = 1
    Right = 2


class SpecialMoveActionType(Enum):
    NOOP = 0
    Jump = 1
    LongJump = 2
    Dash = 3


class GrabActionType(Enum):
    NOOP = 0
    Grab = 1


class StepAction:

    def __init__(self, ):
        self.up_down_action = UpDownActionType.NOOP  # type: UpDownActionType
        self.left_right_action = LeftRightActionType.NOOP  # type: LeftRightActionType
        self.special_move_action = SpecialMoveActionType.NOOP  # type: SpecialMoveActionType
        self.grab_action = GrabActionType.NOOP  # type: GrabActionType

    def build(self) -> np.array:
        return np.array([self.up_down_action.value, self.left_right_action.value, self.special_move_action.value,
                         self.grab_action.value])


class DummyCelesteClient(CelesteClient):
    dummy_actions = [
        np.array([0, 1, 1, 0]),
    ]

    def __init__(self, action_queue):
        super().__init__(action_queue)
        self.num_requests = None
        self.actions = pickle.load(open("python_rl/simulated_input.pkl", "rb"))

    def start_training(self):
        while self.actions:
            if self.num_requests == 0:
                time.sleep(0.05)
                continue
            action = self.actions.pop(0)
            self.env.add_action(action)

    def ext_get_action(self):
        # send action to .NET
        # time how long this takes:
        self.num_requests += 1
        action = self.env.action_queue.get()
        self.num_requests -= 1

        return action

    def ext_add_reward(self, reward):
        self.logger.log(logging.INFO, f"Reward: {reward}")

    def ext_add_observation(self, vision, speed_x_y, can_dash, stamina, death_flag, finished_level, target, position,
                            screen_position, is_climbing, on_ground):

        pass


def record_simulated_input():
    actions = []
    import pygame

    pygame.init()
    window = pygame.display.set_mode((300, 300))
    pygame.display.set_caption("Press keys to record actions")
    clock = pygame.time.Clock()
    clock.tick(60)

    while len(actions) < 100:
        clock.tick(5)
        window.fill(0)
        pygame.display.flip()
        pressed_keys = pygame.key.get_pressed()
        for event in pygame.event.get():
            if event.type == pygame.QUIT:
                pygame.quit()
                return

            if event.type != pygame.KEYDOWN:
                continue

        current_action = StepAction()
        if pressed_keys[pygame.K_w]:
            current_action.up_down_action = UpDownActionType.Up
        elif pressed_keys[pygame.K_s]:
            current_action.up_down_action = UpDownActionType.Down
        if pressed_keys[pygame.K_a]:
            current_action.left_right_action = LeftRightActionType.Left
        elif pressed_keys[pygame.K_d]:
            current_action.left_right_action = LeftRightActionType.Right
        if pressed_keys[pygame.K_j]:
            current_action.special_move_action = SpecialMoveActionType.Jump
        elif pressed_keys[pygame.K_i]:
            current_action.special_move_action = SpecialMoveActionType.LongJump
        elif pressed_keys[pygame.K_k]:
            current_action.special_move_action = SpecialMoveActionType.Dash
        if pressed_keys[pygame.K_LSHIFT]:
            current_action.grab_action = GrabActionType.Grab
        action = current_action.build()
        if [int(x) for x in action.tolist()] == [0,0,0,0]:
            continue
        print(action)
        actions.append(action)
    pickle.dump(actions, open("simulated_input.pkl", "wb"))
    pygame.quit()


if __name__ == "__main__":
    record_simulated_input()
