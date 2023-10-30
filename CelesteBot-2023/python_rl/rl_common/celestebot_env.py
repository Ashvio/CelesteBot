from __future__ import annotations

import queue
from typing import SupportsFloat, Any, List

import gymnasium as gym
import numpy as np
from gymnasium.core import ActType, ObsType, RenderFrame, spaces
from ray.rllib import ExternalEnv


class CelesteEnv(gym.Env):
    VISION_SIZE = 10
    ENTITY_MAX_COUNT = 30
    MIN_SPEED = -1000
    MAX_SPEED = 1000

    def __init__(self, off_policy=False, ):
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
        self.observation_space = spaces.Dict({
            "map_entities_vision": spaces.Box(0, self.ENTITY_MAX_COUNT, shape=(self.VISION_SIZE, self.VISION_SIZE),
                                              dtype=np.uint8),
            "speed_x_y": spaces.Box(self.MIN_SPEED, self.MAX_SPEED, shape=(2,), dtype=np.float16),
            "can_dash": spaces.MultiBinary(1),
            "stamina": spaces.Box(0, 121, shape=(1,), ),

        })

    def step(self, action: ActType) -> tuple[ObsType, SupportsFloat, bool, bool, dict[str, Any]]:
        pass

    def reset(self, *, seed: int | None = None, options: dict[str, Any] | None = None) -> tuple[
        ObsType, dict[str, Any]]:
        super().reset(seed=seed)
