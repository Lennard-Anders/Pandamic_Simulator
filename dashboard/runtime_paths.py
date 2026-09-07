"""User-specific locations without a developer's home or installation path."""
import os
import sys
from pathlib import Path


def default_data_dir(environ=None, platform=None, home=None):
    env = os.environ if environ is None else environ
    platform = sys.platform if platform is None else platform
    home = Path.home() if home is None else Path(home)
    if env.get('PANDEMIC_DATA_DIR'):
        return Path(env['PANDEMIC_DATA_DIR']).expanduser()
    if platform == 'win32':
        base = Path(env.get('LOCALAPPDATA') or home / 'AppData' / 'Local')
    elif platform == 'darwin':
        base = home / 'Library' / 'Application Support'
    else:
        base = Path(env.get('XDG_DATA_HOME') or home / '.local' / 'share')
    return base / 'Colossal Order' / 'Cities_Skylines' / 'Addons' / 'Mods' / 'RealTime' / 'Pandemic Data'
