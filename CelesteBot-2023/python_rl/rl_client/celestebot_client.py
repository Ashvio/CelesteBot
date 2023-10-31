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
import queue
import time
import traceback
from collections import OrderedDict
from threading import Thread
from typing import List, SupportsFloat
import socket, errno

import numpy as np
import requests
from ray.rllib.env.policy_client import PolicyClient

from python_rl.rl_common.celestebot_env import CelesteEnv, TerminationEvent

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

    def __init__(self, action_queue=None, worker_number=0):
        # The following line is the only instance, where an actual env will
        # be created in this entire example (including the server side!).
        # This is to demonstrate that RLlib does not require you to create
        # unnecessary env objects within the PolicyClient/Server objects, but
        # that only this following env and the loop below runs the entire
        # training process.
        self.env = CelesteEnv(action_queue)
        # If server has n workers, all ports between 9900 and 990[n-1] should
        # be listened on. E.g. if server has num_workers=2, try 9900 or 9901.
        # Note that no config is needed in this script as it will be defined
        # on and sent from the server.
        session = requests.Session()
        port = _get_available_port(9900)
        self.client = PolicyClient(
            f"http://127.0.0.1:{port}", inference_mode="remote", session=session
        )
        self.current_episode_id = self.client.start_episode(training_enabled=True)
        self._first_reward = True

        # self.observation_processor = Thread(target=self.process_observation_queue)
        # self.observation_processor.start()
        self.python_logs_txt = "python_logs.txt"
        logging.basicConfig(filename=self.python_logs_txt,
                            filemode='w+',
                            datefmt='%H:%M:%S',
                            level=logging.DEBUG)
        self.logger = logging.getLogger('PythonClientLogger')
        self.logger.log(logging.INFO, "Python client started")

    def ext_test(self):
        print("Hello from python")
        while True:
            self.env.add_action(np.array([0, 1, 1, 0]))
            time.sleep(30)

    def ext_add_observation(self, vision, speed_x_y, can_dash, stamina, last_reward, death_flag, finished_level):
        # send observation from .NET to server and get the action and send it to the queue
        logging.log(logging.INFO, f"Current time in milliseconds add obs to py queue: {str(int(time.time() * 1000))} {str(last_reward)} " )
        observation = OrderedDict()
        observation["can_dash"] = np.array([can_dash])
        observation["map_entities_vision"] = np.array(vision)
        observation["speed_x_y"] = np.array(speed_x_y)
        observation["stamina"] = np.array([stamina])

        self.env.observation_queue.put(observation)
        if self._first_reward:
            # we get rewards from the current game state, so we don't want to send the last reward from the previous game state
            self._first_reward = False
            self.env.reward_queue.put(0.0)
        else:
            self.env.reward_queue.put(last_reward)
        if death_flag:
            self.env.termination_event_queue.put(TerminationEvent.DEATH)
        elif finished_level:
            self.env.termination_event_queue.put(TerminationEvent.FINISHED_LEVEL)
        else:
            self.env.termination_event_queue.put(TerminationEvent.NORMAL)

    def ext_get_action(self):
        # send action to .NET
        # time how long this takes:
        action = self.client.get_action(self.current_episode_id, self.env.observation_queue.get())

        return [int(x) for x in action.tolist()]

    def start_training(self):
        # In the following, we will use our external environment (the CartPole
        # env we created above) in connection with the PolicyClient to query
        # actions (from the server if "remote"; if "local" we'll compute them
        # on this client side), and send back observations and rewards.
        try:
            # Start a new episode.
            obs, info = self.env.reset()
            episode_id = self.client.start_episode(training_enabled=True)
            self.logger.log(logging.INFO, "Started episode, observation: " + str(obs))
            rewards = 0.0
            while True:
                # Compute an action randomly (off-policy) and log it.

                # Compute an action locally or remotely (on server).
                # No need to log it here as the action
                # self.logger.log(logging.DEBUG, "Querying action: " + str(episode_id))

                action = self.client.get_action(episode_id, obs)

                # self.logger.log(logging.DEBUG, "Got action: " + str(action))
                # Perform a step in the external simulator (env).

                obs, reward, terminated, truncated, info = self.env.step(action)

                rewards += reward

                # Log next-obs, rewards, and infos.
                # noinspection PyTypeChecker

                self.client.log_returns(episode_id, reward, info=info)
                # Reset the episode if done.
                if terminated or truncated:
                    self.logger.log(logging.INFO, f"Total reward for episode: {rewards}. Episode ended due to: {info}")

                    rewards = 0.0

                    # End the old episode.
                    self.client.end_episode(episode_id, obs)
                    # Tell Madeline to do nothing to get the next observation
                    self.env.add_action(self.env.NOOP_ACTION)
                    # Start a new episode.
                    obs, info = self.env.reset()
                    self._first_reward = True
                    episode_id = self.client.start_episode(training_enabled=True)
        except Exception as e:
            with open(self.python_logs_txt, 'a') as f:
                f.write(str(e))
                f.write(traceback.format_exc())