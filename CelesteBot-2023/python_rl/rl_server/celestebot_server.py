"""
Example of running an RLlib policy server, allowing connections from
external environment running clients. The server listens on
(a simple CartPole env
in this case) against an RLlib policy server listening on one or more
HTTP-speaking ports. See `cartpole_client.py` in this same directory for how
to start any number of clients (after this server has been started).

This script will not create any actual env to illustrate that RLlib can
run w/o needing an internalized environment.

Setup:
1) Start this server:
    $ python cartpole_server.py --num-workers --[other options]
      Use --help for help.
2) Run n policy clients:
    See `cartpole_client.py` on how to do this.

The `num-workers` setting will allow you to distribute the incoming feed over n
listen sockets (in this example, between 9900 and 990n with n=worker_idx-1).
You may connect more than one policy client to any open listen port.
"""

import argparse
import logging
import os
import pathlib
import random
import shutil
from contextlib import closing
from datetime import datetime
import socket

import ray
from ray import tune, air
from ray.air import RunConfig, ScalingConfig, CheckpointConfig
from ray.rllib.algorithms import Algorithm
from ray.rllib.algorithms.ppo import PPOConfig, PPO
from ray.rllib.env.policy_server_input import PolicyServerInput
from ray.rllib.evaluation.collectors.sample_collector import SampleCollector
from ray.rllib.examples.custom_metrics_and_callbacks import MyCallbacks
from ray.tune import sample_from, run
from ray.tune.logger import pretty_print
from ray.tune.registry import get_trainable_cls
from ray.tune.schedulers.pb2 import PB2
from ray.tune.tune import _get_trainable

from python_rl.rl_common.celestebot_env import CelesteEnv

SERVER_ADDRESS = "127.0.0.1"
# In this example, the user can run the policy server with
# n workers, opening up listen ports 9900 - 990n (n = num_workers - 1)
# to each of which different clients may connect.
SERVER_BASE_PORT = 9900  # + worker-idx - 1
CHECKPOINT_BASE_PATH = "checkpoints"
LAST_CHECKPOINT_FILE = "checkpoints/last_checkpoint.out"


# Postprocess the perturbed config to ensure it's still valid used if PBT.
def explore(config):
    # Ensure we collect enough timesteps to do sgd.
    if config["train_batch_size"] < config["sgd_minibatch_size"] * 2:
        config["train_batch_size"] = config["sgd_minibatch_size"] * 2
    # Ensure we run at least one sgd iter.
    if config["lambda"] > 1:
        config["lambda"] = 1
    config["train_batch_size"] = int(config["train_batch_size"])
    return config


def get_cli_args():
    """Create CLI parser and return parsed arguments"""
    parser = argparse.ArgumentParser()

    # Example-specific args.
    parser.add_argument(
        "--port",
        type=int,
        default=SERVER_BASE_PORT,
        help="The base-port to use (on localhost). " f"Default is {SERVER_BASE_PORT}.",
    )
    parser.add_argument(
        "--callbacks-verbose",
        action="store_true",
        help="Activates info-messages for different events on "
             "server/client (episode steps, postprocessing, etc..).",
    )
    parser.add_argument(
        "--num-workers",
        type=int,
        default=1,
        help="The number of workers to use. Each worker will create "
             "its own listening socket for incoming experiences.",
    )
    parser.add_argument(
        "--no-restore",
        action="store_true",
        help="Do not restore from a previously saved checkpoint (location of "
             "which is saved in `last_checkpoint_[algo-name].out`).",
    )

    # General args.

    parser.add_argument("--num-cpus", type=int, default=8)
    parser.add_argument("--resume-tuner-checkpoint", type=str)
    parser.add_argument("--policy-checkpoint", type=str)

    parser.add_argument(
        "--framework",
        choices=["tf", "tf2", "torch"],
        default="torch",
        help="The DL framework specifier.",
    )
    # parser.add_argument(
    #     "--use-lstm",
    #     action="store_true",
    #     help="Whether to auto-wrap the model with an LSTM. Only valid option for "
    #     "--run=[IMPALA|PPO|R2D2]",
    # )
    parser.add_argument(
        "--stop-iters", type=int, default=200, help="Number of iterations to train."
    )
    parser.add_argument(
        "--stop-timesteps",
        type=int,
        default=500000,
        help="Number of timesteps to train.",
    )

    # parser.add_argument(
    #     "--as-test",
    #     action="store_true",
    #     help="Whether this script should be run as a test: --stop-reward must "
    #     "be achieved within --stop-timesteps AND --stop-iters.",
    # )
    parser.add_argument(
        "--no-tune",
        action="store_true",
        help="Run without Tune using a manual train loop instead. Here,"
             "there is no TensorBoard support.",
    )
    # parser.add_argument(
    #     "--local-mode",
    #     action="store_true",
    #     help="Init Ray in local mode for easier debugging.",
    # )

    args = parser.parse_args()
    print(f"Running with following CLI args: {args}")
    return args


NUM_WORKERS = 3



if __name__ == "__main__":
    args = get_cli_args()
    ray.init()

    PORT = 9900
    BASE_PATH = "C:\projects\CelesteBot\CelesteBot-2023"
    def try_makedirs(path):
        try:
            os.makedirs(os.path.join(BASE_PATH, path))
            return True
        except OSError:
            return False
    def try_rm_dirs(path):
        try:
            os.rmdir(os.path.join(BASE_PATH, path))
            return True
        except OSError:
            return False
    # `InputReader` generator (returns None if no input reader is needed on
    # the respective worker).
    def _input(ioctx):
        # We are remote worker or we are local worker with num_workers=0:
        # Create a PolicyServerInput.
        if ioctx.worker_index >= 0 or ioctx.worker.num_workers == 0:
            base_port = PORT
            while not try_makedirs(str(base_port)):
                base_port += 1
            return PolicyServerInput(
                ioctx,
                SERVER_ADDRESS,
                base_port + ioctx.worker_index - (1 if ioctx.worker_index > 0 else 0),
                idle_timeout=0.025,
                max_sample_queue_size=1000000
            )
        # No InputReader (PolicyServerInput) needed.
        else:
            return None


    env = CelesteEnv(None)

    # Algorithm config. Note that this config is sent to the client only in case
    # the client needs to create its own policy copy for local inference.
    NUM_TRIALS = 3
    config = (
        PPOConfig()
        # Indicate that the Algorithm we setup here doesn't need an actual env.
        # Allow spaces to be determined by user (see below).
        .rl_module(_enable_rl_module_api=False)
        .training(
            _enable_learner_api=False,
            # lr=tune.grid_search([0.01, 0.001, 0.0001])
        )
        .environment(
            env=None,
            # TODO: (sven) make these settings unnecessary and get the information
            #  about the env spaces from the client.
            observation_space=env.observation_space,
            action_space=env.action_space,
        )
        # .update_from_dict({"num_gpus_per_worker": 1})
        # DL framework to use.

        .framework("torch")
        .resources(
            # num_cpus_per_worker=10 // (NUM_WORKERS * NUM_TRIALS),
            # num_cpus_per_worker=1,
            # local_gpu_idx=0,
            # num_cpus_for_local_worker=1,
            # num_cpus_per_learner_worker=1,
            # num_gpus_per_learner_worker=0.2,
            num_gpus_per_worker=1 / (NUM_WORKERS),
            # num_gpus_per_worker=0.05,
            # num_learner_workers=1
            # num_gpus=1,
        )
        # Create a "chatty" client/server or not.
        # .callbacks(MyCallbacks if args.callbacks_verbose else None)
        # Use the `PolicyServerInput` to generate experiences.
        .offline_data(input_=_input, offline_sampling=False, shuffle_buffer_size=0)
        # Use n worker processes to listen on different ports.
        .rollouts(
            num_rollout_workers=0,
            # Connectors are not compatible with the external env.
            enable_connectors=False,
            # create_env_on_local_worker=True,
            # batch_mode="truncate_episodes",
            # rollout_fragment_length='auto',
            remote_env_batch_wait_ms=20,
            # remote_worker_envs=True,
            # num_envs_per_worker=3
        )
        # Disable OPE, since the rollouts are coming from online clients.
        .evaluation(off_policy_estimation_methods={})
        # Set to INFO so we'll see the server's actual address:port.
        .debugging(log_level="INFO")
    )
    lr_start = 2.5e-3
    lr_end = 2.5e-5
    lr_time = 50 * 1000000
    # Example of using PPO (does NOT support off-policy actions).
    hyper_parameter_ranges = {
        "gamma": [0.93, 0.999],
        "lambda": [0.9, 1.0],
        "clip_param": [0.1, 0.2],
        "lr": [1e-5, 1e-3],
    }
    pb2 = PB2(
        time_attr="timesteps_total",
        metric="episode_reward_mean",
        mode="max",
        perturbation_interval=50000,
        quantile_fraction=0.25,  # copy bottom % with top %
        # Specifies the hyperparam search space
        hyperparam_bounds=hyper_parameter_ranges
    )
    config.update_from_dict(
        {
            # "enable_connectors": False,
            # "num_cpus": 22,
            "num_workers": 0,
            "model": {
                "use_lstm": False,
                "use_attention": True,
                "dim": CelesteEnv.VISION_SIZE,
                "conv_filters": [[16, [3, 3], 2], [32, [3, 3], 2], [64, [3, 3], 1], [128, [3, 3], 1]],
                "attention_dim": 128,
                "attention_head_dim": 64,
                "attention_num_transformer_units": 3,
                "attention_memory_inference": 100,
                "attention_memory_training": 100,
                "attention_num_heads": 3,
                # "attention_use_n_prev_actions": 6,
                # "entropy_coeff_schedule": [[0, 1e-3]]
            },
            "entropy_coeff": 1e-2,
            "count_steps_by": "env_steps",
            "num_sgd_iter": 10,
            "sgd_minibatch_size": 64,
            "gamma": sample_from(lambda spec: random.uniform(*hyper_parameter_ranges["gamma"])),
            # "lambda": sample_from(lambda spec: random.uniform(0.9, 1.0)),
            "lambda": 0.96,
            "clip_param": sample_from(lambda spec: random.uniform(*hyper_parameter_ranges["clip_param"])),
            # "lr": sample_from(lambda spec: random.uniform(1e-3, 1e-5)),
            "train_batch_size": 512,
            "lr_schedule": [[0, 1e-3], [100000, 1e-4], [10000000, 1e-5]],
            # "normalize_actions": False
        }
    )

    # Attempt to restore from checkpoint, if possible.
    # Attempt to restore from checkpoint, if possible.
    checkpoint_path = ""

    if not args.no_restore and os.path.exists(LAST_CHECKPOINT_FILE):
        checkpoint_path = open(LAST_CHECKPOINT_FILE).read()
    # Manual training loop (no Ray tune).
    if args.no_tune:
        algo = config.build()

        if checkpoint_path:
            print("Restoring from checkpoint path", checkpoint_path)
            try:
                algo.restore(checkpoint_path)
            except Exception as e:
                logging.log(logging.ERROR, "Failed to restore from checkpoint path " + checkpoint_path)
                logging.log(logging.ERROR, e)

        # Serving and training loop.
        ts = 0

        while True:

            results = algo.train()
            print(pretty_print(results))
            checkpoint = algo.save().checkpoint
            print("Last checkpoint", checkpoint)
            # copy file with same name to path
            logging.log(logging.INFO, "Copying checkpoint to " + CHECKPOINT_BASE_PATH)
            path = pathlib.PurePath(checkpoint_path)

            shutil.copytree(checkpoint.path, os.path.join(CHECKPOINT_BASE_PATH, path.name), dirs_exist_ok=True)
            with open(LAST_CHECKPOINT_FILE, "w") as f:
                f.write(checkpoint.path)

            if ts >= args.stop_timesteps:
                break
            ts += results["timesteps_total"]

        algo.stop()

    # Run with Tune for auto env and algo creation and TensorBoard.
    else:
        print("Ignoring restore even if previous checkpoint is provided...")


        logging.getLogger('requests').setLevel(logging.WARN)
        # TODO: Figure out why connections keep closing (HTTP BaseHandler is 1.0 not 1.1)
        logging.getLogger('urllib3.connectionpool').setLevel(logging.WARN)
        logging.getLogger('urllib3').setLevel(logging.WARN)
        NUM_TRAINER_WORKERS = 3
        NUM_SAMPLES = 3
        if args.policy_checkpoint:
            trainable = Algorithm.from_checkpoint(args.policy_checkpoint).train
            tune.register_trainable("PPO", trainable.train)
            print(trainable)
        ppo = _get_trainable("PPO")
        resources = tune.PlacementGroupFactory([{"CPU": 4, "GPU": 0.33}], strategy="SPREAD")
        trainable = tune.with_resources(ppo, resources)

        # ScalingConfig(
        #     # Number of distributed workers.
        #     num_workers=3,
        #     # Turn on/off GPU.
        #     use_gpu=True,
        #     # Specify resources used for trainer.
        #     trainer_resources={"CPU": 16 // NUM_TRAINER_WORKERS, "GPU": 1 / 9 },
        #     resources_per_worker={"CPU": 2, "GPU": 1 / 18},
        #     # Try to schedule workers on different nodes.
        #     # placement_strategy="SPREAD",
        # ))
        # tune.Tuner.restocre(r"C:\Users\Ashvin\ray_results\PPO_2023-11-01_00-23-59","PPO", param_space=config,).fit()
        base_port = PORT
        stop = {
            "training_iteration": 2e7,
            "episode_reward_mean": 400,
        }
        import sys

        sys.stdout.isatty = lambda: False
        while try_rm_dirs(str(base_port)):
            base_port += 1
        try:
            time = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
            tune_config = tune.TuneConfig(scheduler=pb2, num_samples=NUM_SAMPLES, )
            checkpoint_config = CheckpointConfig(num_to_keep=3, checkpoint_score_attribute="episode_reward_mean", checkpoint_frequency=100000)
            run_config = RunConfig(stop=stop, verbose=2, name=f"CelesteBot_{time}", log_to_file=True, checkpoint_config=checkpoint_config)
            if args.resume_tuner_checkpoint:
                # analysis = run(
                #     trainable_with_resources, config=config, scheduler=pb2, stop=stop, verbose=1, resume=True,
                #     name=args.resume_checkpoint, num_samples=3
                # )
                tuner = tune.Tuner.restore(args.resume_tuner_checkpoint, trainable, param_space=config, restart_errored=True).fit()
            else:

                tuner = tune.Tuner(
                    trainable,
                    tune_config=tune_config,
                    # param_space={"lr": 0.0001},
                    param_space=config,
                    run_config=run_config,
                    # stop=stop,
                    # name=f"CelesteBot_{time}"
                ).fit()
        finally:
            base_port = PORT
            while try_rm_dirs(str(base_port)):
                base_port += 1

                # analysis = run(
            #     trainable_with_resources,  scheduler=pb2, stop=stop, verbose=1, num_samples=4,
            #     name=f"CelesteBot_{time}"
            # )
