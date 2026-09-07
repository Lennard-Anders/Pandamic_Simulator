import unittest
from pathlib import Path
try:
    from .runtime_paths import default_data_dir
except ImportError:
    from runtime_paths import default_data_dir


class RuntimePathsTests(unittest.TestCase):
    def test_redirected_windows_profile(self):
        actual = default_data_dir({'LOCALAPPDATA': '/redirected/profile'}, 'win32', '/unused')
        self.assertEqual(actual, Path('/redirected/profile/Colossal Order/Cities_Skylines/Addons/Mods/RealTime/Pandemic Data'))

    def test_explicit_output_root_wins(self):
        self.assertEqual(default_data_dir({'PANDEMIC_DATA_DIR': '/my/experiments'}, 'win32'), Path('/my/experiments'))

    def test_other_platform_data_roots(self):
        self.assertTrue(str(default_data_dir({}, 'darwin', '/user')).startswith(str(Path('/user/Library/Application Support'))))
        self.assertTrue(str(default_data_dir({'XDG_DATA_HOME': '/data'}, 'linux', '/user')).startswith(str(Path('/data'))))
