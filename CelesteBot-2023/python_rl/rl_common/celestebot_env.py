from __future__ import annotations

import logging
import queue
import time
from enum import Enum
from typing import SupportsFloat, Any, List, OrderedDict

import gymnasium as gym
import numpy as np
from gymnasium.core import ActType, ObsType, RenderFrame, spaces
from ray.rllib import ExternalEnv
from ray.rllib.models.preprocessors import get_preprocessor


class TerminationEvent(Enum):
    NORMAL = 0
    DEATH = 1
    FINISHED_LEVEL = 2


class CelesteEnv(gym.Env):
    VISION_SIZE = 40
    ENTITY_MAX_COUNT = 255
    MIN_SPEED = -1000
    MAX_SPEED = 1000
    MIN_POSITION = -50000
    MAX_POSITION = 50000
    NOOP_ACTION = np.array([0, 0, 0, 0, 3])

    def __init__(self, action_queue=None, off_policy=False, logger=None):
        """
        Action Space
        Up/Down: 3
            NOOP
            Up
            Down
        Left/Right: 3
            NOOP
            Left
            Right
        Jump/Dash: 4
            NOOP
            Jump
            Long jump
            Dash
        Climb: 2
            NOOP
            Climb
        Number of frames between next action:
            [4, 10] => 6 discrete plus 4


        Observation Space
        Vision: 2D array of entity objects, shape of 10 by 10 with 30 possibilities per entry

        SpeedX: (-1000, 1000)
        SpeedY: (-1000, 1000)

        CanDash: [0 or 1]
        Stamina: (-1, 120)
        """
        self.logger = logger
        self.off_policy = off_policy
        self.action_space = spaces.MultiDiscrete([3, 3, 4, 2, 5])
        shape = (self.VISION_SIZE, self.VISION_SIZE, 1)

        self.observation_space = spaces.Dict({
            "can_dash": spaces.MultiBinary(1),
            "is_climbing": spaces.MultiBinary(1),
            "map_entities_vision": spaces.Box(0, self.ENTITY_MAX_COUNT, shape=shape,
                                              dtype=np.uint8),
            "on_ground": spaces.MultiBinary(1),
            "position": spaces.Box(self.MIN_POSITION, self.MAX_POSITION, shape=(2,), dtype=np.float32),
            "screen_position": spaces.Box(self.MIN_POSITION, self.MAX_POSITION, shape=(2,), dtype=np.float32),
            "speed_x_y": spaces.Box(self.MIN_SPEED, self.MAX_SPEED, shape=(2,), dtype=np.float16),
            "stamina": spaces.Box(-10, 10, shape=(1,), ),
            "target": spaces.Box(self.MIN_POSITION, self.MAX_POSITION, shape=(2,), dtype=np.float32),
        })
        self.observation_queue = queue.Queue()  # type: queue.Queue[OrderedDict]
        self.reward_queue = queue.Queue()  # type: queue.Queue[float]
        self.action_queue = action_queue  # type: queue.Queue[List[int]]
        self.termination_event_queue = queue.Queue()  # type: queue.Queue[TerminationEvent]

    def add_action(self, action):
        self.action_queue.put([int(x) for x in action.tolist()])

    def get_reward(self):
        try:
            reward = self.reward_queue.get(timeout=0.5)
        except queue.Empty:
            self.logger.log(logging.INFO, "Timed out waiting for reward")
            self.logger.log(logging.INFO, "queue size: " + str(self.reward_queue.qsize()))
            reward = 0.0
        # self.logger.log(logging.INFO, "Reward: " + str(reward))
        # info = self.info_queue.get()
        # self.episode_rewards += reward
        # self.info_queue.task_done()
        return reward

    def step(self, action: ActType) -> tuple[ObsType, SupportsFloat, bool, bool, dict[str, Any]]:
        # Doesnt return reward yet
        self.add_action(action)
        observation = self.observation_queue.get()

        # reward = self.reward_queue.get()
        reward = self.get_reward()
        termination_event = self.termination_event_queue.get()
        termination = termination_event == TerminationEvent.DEATH or termination_event == TerminationEvent.FINISHED_LEVEL
        return observation, reward, termination, False, {"Died": termination_event == TerminationEvent.DEATH,
                                                    "Finished Level": termination_event == TerminationEvent.FINISHED_LEVEL}

    def reset(self, *, seed: int | None = None, options: dict[str, Any] | None = None) -> tuple[
        ObsType, dict[str, Any]]:
        super().reset(seed=seed)
        return self.observation_queue.get(), {}


if __name__ == "__main__":
    env = CelesteEnv()
    # preprocessor = get_preprocessor(env.observation_space)
    # preprocessor.
    # print(env.observation_space.shape)
    print(env.observation_space.sample())