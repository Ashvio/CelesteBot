import queue
from typing import SupportsFloat, Any

import gymnasium as gym
import numpy as np
from gymnasium.core import ActType, ObsType, RenderFrame, spaces
from ray.rllib import ExternalEnv


class CelesteEnv(ExternalEnv):
    VISION_SIZE = 10
    ENTITY_MAX_COUNT = 30
    MIN_SPEED = -1000
    MAX_SPEED = 1000

    def __init__(self):
        """
        Action Space
        Basic: 9
            NOOP
            Left
            Right
            Up
            Down
            Up+Left
            Up+Right
            Down+Left
            Down+Right
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
        action_space = spaces.MultiDiscrete([9, 3, 2])
        observation_space = spaces.Dict({
            "map_entities_vision": spaces.Box(0, self.ENTITY_MAX_COUNT, shape=(self.VISION_SIZE, self.VISION_SIZE),
                                              dtype=np.uint8),
            "speed_x_y": spaces.Box(self.MIN_SPEED, self.MAX_SPEED, shape=(2,), dtype=np.float16),
            "can_dash": spaces.MultiBinary(1),
            "stamina": spaces.Discrete(121, start=-1),

        })
        self.game_state_queue = queue.Queue()

        super().__init__(action_space, observation_space)

    def _get_celeste_gamestate(self):
        game_state = self.game_state_queue.get(timeout=30)
        return game_state
    def run(self):
        """Override this to implement the run loop.

                Your loop should continuously:
                    1. Call self.start_episode(episode_id)
                    2. Call self.[get|log]_action(episode_id, obs, [action]?)
                    3. Call self.log_returns(episode_id, reward)
                    4. Call self.end_episode(episode_id, obs)
                    5. Wait if nothing to do.

                Multiple episodes may be started at the same time.
        """
        while True:
            game_state = self._get_celeste_gamestate()
            self.start_episode(training_enabled=True)
