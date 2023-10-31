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


class TerminationEvent(Enum):
    NORMAL = 0
    DEATH = 1
    FINISHED_LEVEL = 2


class CelesteEnv(gym.Env):
    VISION_SIZE = 20
    ENTITY_MAX_COUNT = 100
    MIN_SPEED = -1000
    MAX_SPEED = 1000
    NOOP_ACTION = np.array([0, 0, 0, 0])

    def __init__(self, action_queue=None, off_policy=False, ):
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
        Jump/Dash: 3
            NOOP
            Jump
            Dash
        Climb: 2
            NOOP
            Climb

        Observation Space
        Vision: 2D array of entity objects, shape of 10 by 10 with 30 possibilities per entry

        SpeedX: (-1000, 1000)
        SpeedY: (-1000, 1000)

        CanDash: [0 or 1]
        Stamina: (-1, 120)
        """
        self.off_policy = off_policy
        self.action_space = spaces.MultiDiscrete([3, 3, 3, 2])
        shape = (self.VISION_SIZE, self.VISION_SIZE)
        low = np.full(shape, 0)  # 2D array filled with 0s
        high = np.full(shape, self.ENTITY_MAX_COUNT)  # 2D array filled with 30s
        self.observation_space = spaces.Dict({
            "can_dash": spaces.MultiBinary(1),
            "map_entities_vision": spaces.Box(low, high, shape=shape,
                                              dtype=np.uint16),
            "speed_x_y": spaces.Box(self.MIN_SPEED, self.MAX_SPEED, shape=(2,), dtype=np.float16),
            "stamina": spaces.Box(-1, 1, shape=(1,), ),
        })
        self.observation_queue = queue.Queue()  # type: queue.Queue[OrderedDict]
        self.reward_queue = queue.Queue()  # type: queue.Queue[float]
        self.action_queue = action_queue  # type: queue.Queue[List[int]]
        self.termination_event_queue = queue.Queue()  # type: queue.Queue[TerminationEvent]

    def add_action(self, action):
        self.action_queue.put([int(x) for x in action.tolist()])

    def step(self, action: ActType) -> tuple[ObsType, SupportsFloat, bool, bool, dict[str, Any]]:
        self.add_action(action)
        observation = self.observation_queue.get()
        logging.log(logging.INFO, "Current time in milliseconds after getting obs: " + str(int(time.time() * 1000)))

        reward = self.reward_queue.get()
        termination_event = self.termination_event_queue.get()
        termination = termination_event == TerminationEvent.DEATH or termination_event == TerminationEvent.FINISHED_LEVEL
        return observation, reward, termination, False, {"Died": termination_event == TerminationEvent.DEATH,
                                                         "Finished Level": termination_event == TerminationEvent.FINISHED_LEVEL}

    def reset(self, *, seed: int | None = None, options: dict[str, Any] | None = None) -> tuple[
        ObsType, dict[str, Any]]:
        super().reset(seed=seed)
        return self.observation_queue.get(), {}
