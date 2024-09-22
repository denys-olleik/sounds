using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.Wave;

namespace Sounds
{
  public partial class Form1 : Form
  {
    private IntPtr _hookID = IntPtr.Zero;
    private LowLevelKeyboardProc _proc;
    private readonly object _lockObj = new object();
    private int _maxConcurrentSounds = 10;
    private WaveOutEvent[] _outputDevices;
    private AudioFileReader[] _audioFiles;
    private bool[] _isPlaying;

    private string _defaultSoundFilePath = "villager_woodcutter1.WAV";
    private string _enterSoundFilePath = "cavalry_attack2.WAV";
    private string _spaceSoundFilePath = "villager_stoneminer1.WAV";
    private string _copySoundFilePath = "SOUND108.WAV"; // Add this sound file for Ctrl+C
    private string _pasteSoundFilePath = "SOUND108.WAV"; // Add this sound file for Ctrl+V

    private bool _ctrlPressed = false;

    public Form1()
    {
      InitializeComponent();
      _proc = HookCallback;
      InitializeSoundPool();
    }

    private void Form1_Load(object sender, EventArgs e)
    {
      _hookID = SetHook(_proc);
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
      UnhookWindowsHookEx(_hookID);
      DisposeSoundPool();
    }

    private void InitializeSoundPool()
    {
      _outputDevices = new WaveOutEvent[_maxConcurrentSounds];
      _audioFiles = new AudioFileReader[_maxConcurrentSounds];
      _isPlaying = new bool[_maxConcurrentSounds];

      for (int i = 0; i < _maxConcurrentSounds; i++)
      {
        _audioFiles[i] = new AudioFileReader(_defaultSoundFilePath);
        _outputDevices[i] = new WaveOutEvent();
        _outputDevices[i].Init(_audioFiles[i]);
        _outputDevices[i].PlaybackStopped += (sender, args) =>
        {
          lock (_lockObj)
          {
            for (int j = 0; j < _maxConcurrentSounds; j++)
            {
              if (sender == _outputDevices[j])
              {
                _isPlaying[j] = false;
                _audioFiles[j].Position = 0;
                break;
              }
            }
          }
        };
      }
    }

    private void DisposeSoundPool()
    {
      for (int i = 0; i < _maxConcurrentSounds; i++)
      {
        _outputDevices[i]?.Dispose();
        _audioFiles[i]?.Dispose();
      }
    }

    private void PlaySound(Keys key)
    {
      string soundFilePath = key switch
      {
        Keys.Enter => _enterSoundFilePath,
        Keys.Space => _spaceSoundFilePath,
        _ => _defaultSoundFilePath
      };

      lock (_lockObj)
      {
        for (int i = 0; i < _maxConcurrentSounds; i++)
        {
          if (!_isPlaying[i])
          {
            _audioFiles[i].Dispose();
            _audioFiles[i] = new AudioFileReader(soundFilePath);
            _outputDevices[i].Init(_audioFiles[i]);
            _isPlaying[i] = true;
            _outputDevices[i].Play();
            break;
          }
        }
      }
    }

    private void PlaySound(string soundFilePath)
    {
      lock (_lockObj)
      {
        for (int i = 0; i < _maxConcurrentSounds; i++)
        {
          if (!_isPlaying[i])
          {
            _audioFiles[i].Dispose();
            _audioFiles[i] = new AudioFileReader(soundFilePath);
            _outputDevices[i].Init(_audioFiles[i]);
            _isPlaying[i] = true;
            _outputDevices[i].Play();
            break;
          }
        }
      }
    }

    private IntPtr SetHook(LowLevelKeyboardProc proc)
    {
      using (Process curProcess = Process.GetCurrentProcess())
      using (ProcessModule curModule = curProcess.MainModule)
      {
        return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
            GetModuleHandle(curModule.ModuleName), 0);
      }
    }

    private delegate IntPtr LowLevelKeyboardProc(
        int nCode, IntPtr wParam, IntPtr lParam);

    private IntPtr HookCallback(
        int nCode, IntPtr wParam, IntPtr lParam)
    {
      if (nCode >= 0 && (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP))
      {
        int vkCode = Marshal.ReadInt32(lParam);
        Keys key = (Keys)vkCode;

        if (key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey)
        {
          _ctrlPressed = false;
        }
        else if (_ctrlPressed && key == Keys.C)
        {
          Task.Run(() => PlaySound(_copySoundFilePath));
        }
        else if (_ctrlPressed && key == Keys.V)
        {
          Task.Run(() => PlaySound(_pasteSoundFilePath));
        }
        else
        {
          Task.Run(() => PlaySound(key));
        }
      }
      else if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
      {
        int vkCode = Marshal.ReadInt32(lParam);
        Keys key = (Keys)vkCode;

        if (key == Keys.ControlKey || key == Keys.LControlKey || key == Keys.RControlKey)
        {
          _ctrlPressed = true;
        }
      }

      return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    #region WinAPI Functions and Constants

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYUP = 0x0105;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook,
        LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
        IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    #endregion
  }
}
