import argparse
import math
import os
import platform
import subprocess
import time

from pywinauto import application

CELESTE_PATH = os.environ["CELESTE_PATH"]
CELESTE_BASE_PATH = os.path.dirname(CELESTE_PATH)

# customize for your monitor
SCREEN_WIDTH = 2560
SCREEN_HEIGHT = 1440


def get_cli_args():
    """Create CLI parser and return parsed arguments"""
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--num-workers",
        type=int,
        default=1,
        help="The number of workers.",
    )
    parser.add_argument(
        "--worker-index-offset",
        type=int,
        default=0,
        help="The number of workers.",
    )

    args = parser.parse_args()
    print(f"Running with following CLI args: {args}")
    return args


if __name__ == "__main__":
    if platform.system() != "Windows":
        raise OSError("This script is only for Windows")

    args = get_cli_args()
    # Create Celeste worker folders by copying original folder and contents
    os.environ["NUM_CLIENT_WORKERS"] = str(args.num_workers)
    # Get Celeste worker path by stepping back one folder from CELESTE PATH and creating directory called CelesteWorkers
    home_path = os.path.dirname(CELESTE_BASE_PATH)
    celeste_worker_path = os.path.join(home_path, "CelesteWorkers")
    print(home_path)
    os.makedirs(celeste_worker_path, exist_ok=True)
    executables = []
    for i in range(args.worker_index_offset, args.num_workers):
        worker_path = os.path.join(celeste_worker_path, f"Celeste_{i}")
        print("worker_path", worker_path)
        os.makedirs(worker_path, exist_ok=True)
        # Copy contents of CELESTE_PATH to worker_path using  xcopy /m src\* dest
        print(f"Copying contents of {CELESTE_BASE_PATH} to {worker_path}")
        p = subprocess.run(['robocopy', '/MIR', '/IM', f"{CELESTE_BASE_PATH}", f"{worker_path} "], capture_output=True,
                           text=True)
        executables.append(os.path.join(worker_path, "Celeste.exe"))
        print(p.args)

        print(p.stdout)
        print(p.stderr)

    # Run N processes from the respective executable
    processes = []
    for executable in executables:
        print(f"Starting {executable}")
        p = subprocess.Popen(executable)
        processes.append(p)
        time.sleep(0.25)
    # Wait for processes to open windows
    time.sleep(max(3 * args.num_workers, 30))
    x = 0
    y = 0
    print(f"Moving {args.num_workers} windows")
    total_screen_size = SCREEN_WIDTH * SCREEN_HEIGHT
    size_per_app = math.ceil(total_screen_size / args.num_workers)
    width_to_height_ratio = 96 / 54
    num_rows = math.ceil(math.sqrt(args.num_workers))
    app_box_width = math.sqrt(size_per_app)
    if args.num_workers > 4:

        app_width = SCREEN_WIDTH // num_rows
        app_height = SCREEN_HEIGHT // num_rows
    else:
        app_width = 960
        app_height = 540
    width_left = SCREEN_WIDTH
    apps = []
    # gamepad = vg.VX360Gamepad()
    try:
        for i in range(num_rows):
            for j in range(num_rows):
                if i * num_rows + j >= args.num_workers:
                    break
                process = processes[i * num_rows + j - args.worker_index_offset]
                app = application.Application()
                app.connect(process=process.pid)
                window = app.window()
                if args.num_workers > 4:
                    window.move_window(x, y, app_width, app_height, repaint=False)
                else:
                    # dont increase window size
                    window.move_window(x, y, repaint=False)
                x += app_width
                width_left -= x
                apps.append(app)
            y += app_height
            x = 0
            width_left = SCREEN_WIDTH
    except:
        for process in processes:
            process.kill()
        raise
    # doing this in C# now
    # for app in apps:
    #     window = app.window()
    #
    #     window.set_focus()
    #     window.set_keyboard_focus()
    #     time.sleep(2)
    #
    #     gamepad.press_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_A)
    #     gamepad.update()
    #
    #     time.sleep(0.1)
    #     gamepad.release_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_A)
    #
    #     # gamepad.press_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_A)
    #     time.sleep(1)
    #     # move down to Randomizer
    #     print('down')
    #     gamepad.press_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_DPAD_DOWN)
    #     gamepad.update()
    #     time.sleep(0.1)
    #
    #     gamepad.release_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_DPAD_DOWN)
    #     gamepad.update()
    #     print('spam')
    #     for i in range(15):
    #         # Start the game
    #         time.sleep(0.05)
    #         gamepad.press_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_A)
    #         gamepad.update()
    #         time.sleep(0.05)
    #         gamepad.release_button(vg.XUSB_BUTTON.XUSB_GAMEPAD_A)
    #         gamepad.update()
    #         time.sleep(0.05)
    #     gamepad.reset()
    #     time.sleep(2)