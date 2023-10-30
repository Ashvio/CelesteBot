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
import queue
import time
from collections import OrderedDict
from threading import Thread
from typing import List

import numpy as np
from ray.rllib.env.policy_client import PolicyClient

from python_rl.rl_common.celestebot_env import CelesteEnv

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


class CelesteClient:

    def __init__(self, action_queue=None, observation_queue=None, worker_number=0):
        # The following line is the only instance, where an actual env will
        # be created in this entire example (including the server side!).
        # This is to demonstrate that RLlib does not require you to create
        # unnecessary env objects within the PolicyClient/Server objects, but
        # that only this following env and the loop below runs the entire
        # training process.
        self.env = CelesteEnv(False)
        # If server has n workers, all ports between 9900 and 990[n-1] should
        # be listened on. E.g. if server has num_workers=2, try 9900 or 9901.
        # Note that no config is needed in this script as it will be defined
        # on and sent from the server.
        self.client = PolicyClient(
            f"http://localhost:{9900 + worker_number}", inference_mode="local"
        )
        self.current_episode_id = self.client.start_episode(training_enabled=True)
        self.observation_queue = observation_queue  # type: queue.Queue[OrderedDict]
        self.action_queue = action_queue  # type: queue.Queue[List[int]]
        self.observation_processor = Thread(target=self.process_observation_queue)
        self.observation_processor.start()

    def ext_test(self):
        print("Hello from python")
        while True:
            self.add_action(np.array([0, 1, 1, 0]))
            time.sleep(30)

    def ext_add_observation(self, vision, speed_x_y, can_dash, stamina):
        # send observation from .NET to server and get the action and send it to the queue
        observation = OrderedDict({
            "map_entities_vision": np.array(vision),
            "speed_x_y": np.array(speed_x_y),
            "can_dash": np.int(can_dash),
            "stamina": np.int(stamina)
        })
        self.observation_queue.put(observation)

    def ext_get_action(self):
        # send action to .NET
        action = self.client.get_action(self.current_episode_id, self.observation_queue.get())
        return [int(x) for x in action.tolist()]

    def process_observation_queue(self):
        while True:
            observation = self.observation_queue.get()
            action = self.client.get_action(self.current_episode_id, observation)
            self.add_action(action)

    def add_action(self, action):
        self.action_queue.put([int(x) for x in action.tolist()])

    def start_training(self):
        # In the following, we will use our external environment (the CartPole
        # env we created above) in connection with the PolicyClient to query
        # actions (from the server if "remote"; if "local" we'll compute them
        # on this client side), and send back observations and rewards.

        # Start a new episode.
        obs, info = self.env.reset()
        eid = self.client.start_episode(training_enabled=True)

        rewards = 0.0
        while True:
            # Compute an action randomly (off-policy) and log it.

            # Compute an action locally or remotely (on server).
            # No need to log it here as the action
            action = self.client.get_action(eid, obs)

            # Perform a step in the external simulator (env).
            obs, reward, terminated, truncated, info = self.env.step(action)
            rewards += reward

            # Log next-obs, rewards, and infos.
            self.client.log_returns(eid, reward, info=info)

            # Reset the episode if done.
            if terminated or truncated:
                print("Total reward:", rewards)

                rewards = 0.0

                # End the old episode.
                self.client.end_episode(eid, obs)

                # Start a new episode.
                obs, info = self.env.reset()
                eid = self.client.start_episode(training_enabled=True)
