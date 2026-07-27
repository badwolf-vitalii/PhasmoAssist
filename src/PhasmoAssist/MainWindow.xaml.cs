using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Enum;
using Newtonsoft.Json;

namespace PhasmoAssist
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
#if DEBUG
        const string TargetProcessName = "Notepad";
        const bool HideWhenNotActive = false;
#else
        const string TargetProcessName = "Phasmophobia";
        const bool HideWhenNotActive = true;
#endif

        private GlobalInputHook _keyboardHook;
        private bool _isVisible { get; set; }
        private bool _isTemporarilyHidden { get; set; } // hidden and waiting for the TargetProcessName
        private bool _isFocused { get; set; } // this process or the TargetProcessName is focused
        private bool _isGameTimerReset { get; set; }
        private bool _isTimerReset { get; set; }
        private string? _languageFileName { get; set; }
        private static string[]? _language { get; set; }
        private int _timerFontSizeInitial = 35;
        private int _timerFontSizeAbove20 = 50;
        private int _evidencesFontSize = 35;
        private int _ghostsFontSize = 35;
        private int _ghostsFontSizeIdentified = 60;
        private OffsetStopwatch _swGame { get; set; }
        private OffsetStopwatch _sw { get; set; }
        private OffsetStopwatch _sw2 { get; set; }
        private OffsetStopwatch _swLastTimer { get; set; }
        private TimeSpan _elapsedSw { get; set; }
        private CancellationTokenSource? _gameTimerToken { get; set; }
        private CancellationTokenSource? _assistTimerToken { get; set; }
        private CancellationTokenSource? _blinkTextToken { get; set; }
        private GameStatistic _statistic {  get; set; }

        private Key _specialKey
        {
            get => GlobalInputHook.SpecialKey;
            set => GlobalInputHook.SpecialKey = value;
        }

        private Key _shortcutKet
        {
            get => GlobalInputHook.ShortcutKey;
            set => GlobalInputHook.ShortcutKey = value;
        }

        private List<Ghost>? _ghosts { get; set; }
        private List<Ghost>? _ghostsBackup { get; set; }
        private Dictionary<EEvidence, bool> _evidences = new Dictionary<EEvidence, bool>();
        private Dictionary<EEvidence, bool> _evidencesBackup = new Dictionary<EEvidence, bool>();
        
        private static Dictionary<Key, int> _keys = new Dictionary<Key, int>();

        public MainWindow()
        {
            var processName = Process.GetCurrentProcess().ProcessName;
            var processCount = Process.GetProcessesByName(processName).Count();
            if (processCount > 1)
            {
                Application.Current.Shutdown();
            }

            InitializeComponent();

            _isVisible = true;
            _isGameTimerReset = true;
            _isTimerReset = true;
            
            Hide();
            _isTemporarilyHidden = true;
            _isFocused = false;

            var timerOfVisibility = new DispatcherTimer();
            timerOfVisibility.Interval = TimeSpan.FromMilliseconds(500);
            timerOfVisibility.Tick += CheckVisibilityOnTimerTick;
            timerOfVisibility.Start();

            var timerOfFocus = new DispatcherTimer();
            timerOfFocus.Interval = TimeSpan.FromMilliseconds(250);
            timerOfFocus.Tick += CheckFocusOnTimerTick;
            timerOfFocus.Start();

            LoadLanguage();
            StartBlinking();
            LoadConfig();
            InitGhosts();
            LoadEvidences();
            LoadKeys();

            _swGame = new OffsetStopwatch();
            _sw = new OffsetStopwatch();
            _sw2 = new OffsetStopwatch();
            _swLastTimer = new OffsetStopwatch();
            _keyboardHook = new GlobalInputHook();
            _keyboardHook.KeyPressed += OnGlobalKeyPressed;
            _keyboardHook.MouseButtonChanged += OnMouseButtonChanged;

            _statistic = new GameStatistic();

#if RELEASE
            LaunchTheGame();
#endif
        }

        private void LoadLanguage()
        {
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var languagesDir = Path.Combine(root!, "Languages");
            if (Directory.Exists(languagesDir))
            {
                var files = Directory.GetFiles(languagesDir, "*.txt", SearchOption.TopDirectoryOnly);
                if (files != null && files.Any())
                {
                    var langFileName = Path.Combine(root!, "lang.inf");
                    if (File.Exists(langFileName))
                    {
                        var content = File.ReadAllText(langFileName).Trim();
                        langFileName = Path.Combine(languagesDir, content);
                        if (File.Exists(langFileName))
                        {
                            LoadLanguage(langFileName);
                        }
                        else if (files.Length == 1)
                        {
                            LoadLanguage(files.First());
                        }
                        else
                        {
                            langFileName = Path.Combine(languagesDir, "Українська.txt");
                            if (File.Exists(langFileName))
                            {
                                LoadLanguage(langFileName);
                            }
                            else
                            {
                                LoadLanguage(files.First());
                            }
                        }
                    }
                    else
                    {
                        langFileName = Path.Combine(languagesDir, "Українська.txt");
                        if (File.Exists(langFileName))
                        {
                            LoadLanguage(langFileName);
                        }
                        else
                        {
                            LoadLanguage(files.First());
                        }
                    }
                }
            }
        }

        private void StartBlinking()
        {
            _blinkTextToken = new CancellationTokenSource();
            Task.Run(BlinkText);
        }

        private void LoadEvidences()
        {
            var evidences = System.Enum.GetValues(typeof(EEvidence)).Cast<EEvidence>();
            foreach (var evidence in evidences)
            {
                _evidences.Add(evidence, false);
                _evidencesBackup.Add(evidence, false);
            }
        }

        private void LoadLanguage(string fileName)
        {
            if (!File.Exists(fileName))
            {
                var rnd = new Random(DateTime.Now.Millisecond).Next();
                if (rnd % 2 == 0)
                {
                    MessageBox.Show("Відсутній словник", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Dictionary not detected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Application.Current.Shutdown();
            }

            _language = File.ReadAllLines(fileName);
            _languageFileName = fileName;

            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var langFileName = Path.Combine(root!, "lang.inf");
            File.WriteAllText(langFileName, fileName);

            try
            {
                if (_cbs == null)
                {
                    _cbs = new[]
                    {
                        cb0, cb1, cb2, cb3, cb4, cb5, cb6, cb7, cb8, cb9, cb10, cb11, cb12, cb13, cb14, cb15, cb16, cb17, cb18, cb19, cb20
                    };
                }
                btnAllMaps.Content = _language[34];
                btnSmallMaps.Content = _language[35];
                btnMediumMaps.Content = _language[36];
                btnLargeMaps.Content = _language[37];
                btnRandomize.Content = _language[38];
                tbMaps.Text = " " + _language[39] + " ";
                tbInstruments.Text = " " + _language[40] + " ";
                for (var i = 0; i < _cbs.Length; ++i)
                {
                    _cbs[i].Content = _language[i + 41];
                }
            }
            catch
            {
            }
        }

        private void LoadConfig()
        {
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var configFileName = Path.Combine(root!, "config.cfg");
            
            if (File.Exists(configFileName))
            {
                var content = File.ReadAllLines(configFileName);
                for (var i = 1; i < content.Length; i += 2)
                {
                    var line = content[i].Trim();
                    if (!int.TryParse(line, out var value)) value = 0;
                    if (value <= 0) continue;

                    switch (i)
                    {
                        case 1:
                            {
                                _timerFontSizeInitial = value;
                                break;
                            }
                        case 3:
                            {
                                _timerFontSizeAbove20 = value;
                                break;
                            }
                        case 5:
                            {
                                _evidencesFontSize = value;
                                break;
                            }
                        case 7:
                            {
                                _ghostsFontSize = value;
                                break;
                            }
                        case 9:
                            {
                                _ghostsFontSizeIdentified = value;
                                break;
                            }
                        case 11:
                            {
                                if (value == 1)
                                {
                                    _shortcutKet = Key.LeftAlt;
                                }
                                else
                                {
                                    _shortcutKet = Key.LeftCtrl;
                                }
                                break;
                            }
                    }
                    _specialKey = Key.LeftShift;
                }
            }

            tbAssistTimer.FontSize = _timerFontSizeInitial;
            tbEvidences.FontSize = _evidencesFontSize;

            var cfg = new[]
            {
                "[Initial timer font size]",
                _timerFontSizeInitial.ToString(),
                "[Timer font size after 20]",
                _timerFontSizeAbove20.ToString(),
                "[Evidence font size]",
                _evidencesFontSize.ToString(),
                "[Ghosts font size]",
                _ghostsFontSize.ToString(),
                "[Identified ghost font size]",
                _ghostsFontSizeIdentified.ToString(),
                "[Spetial key for shortcuts (1 - LeftAlt, 0 - LeftCtrl, anything else - LeftCtrl)]",
                (_shortcutKet == Key.LeftAlt ? 1 : 0).ToString(),
            };
            File.WriteAllLines(configFileName, cfg);
        }

        private void InitGhosts()
        {
            _ghosts =
            [
                new Ghost
                {
                    GhostType = EGhost.Spirit,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostWriting,
                        EEvidence.EMF5,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Wraith,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.EMF5,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Phantom,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.Ultraviolet,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Poltergeist,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostWriting,
                        EEvidence.Ultraviolet,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Banshee,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.GhostOrb,
                        EEvidence.Ultraviolet
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Jinn,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.EMF5,
                        EEvidence.Ultraviolet,
                        EEvidence.FreezingTemperatures
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Mare,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostWriting,
                        EEvidence.GhostOrb,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Revenant,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostWriting,
                        EEvidence.GhostOrb,
                        EEvidence.FreezingTemperatures
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Shade,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostWriting,
                        EEvidence.EMF5,
                        EEvidence.FreezingTemperatures
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Demon,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostWriting,
                        EEvidence.Ultraviolet,
                        EEvidence.FreezingTemperatures
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Yurei,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.GhostOrb,
                        EEvidence.FreezingTemperatures
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Oni,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.EMF5,
                        EEvidence.FreezingTemperatures
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Yokai,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.GhostOrb,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Hantu,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostOrb,
                        EEvidence.Ultraviolet,
                        EEvidence.FreezingTemperatures
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Goryo,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.EMF5,
                        EEvidence.Ultraviolet
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Aswang,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.FreezingTemperatures,
                        EEvidence.GhostWriting
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Kormos,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostOrb,
                        EEvidence.SpiritBox,
                        EEvidence.Ultraviolet
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Myling,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostWriting,
                        EEvidence.EMF5,
                        EEvidence.Ultraviolet
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Onryo,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostOrb,
                        EEvidence.FreezingTemperatures,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Twins,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.EMF5,
                        EEvidence.FreezingTemperatures,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Raiju,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.EMF5,
                        EEvidence.GhostOrb
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Obake,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.EMF5,
                        EEvidence.GhostOrb,
                        EEvidence.Ultraviolet
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Mimic,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.Ultraviolet,
                        EEvidence.FreezingTemperatures,
                        EEvidence.SpiritBox,
                        EEvidence.GhostOrb
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Moroi,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostWriting,
                        EEvidence.FreezingTemperatures,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Deogen,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.GhostWriting,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Thaye,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.DOTS,
                        EEvidence.GhostWriting,
                        EEvidence.GhostOrb
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Dayan,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.EMF5,
                        EEvidence.GhostOrb,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Obambo,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.GhostWriting,
                        EEvidence.Ultraviolet,
                        EEvidence.DOTS
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Gallu,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.EMF5,
                        EEvidence.Ultraviolet,
                        EEvidence.SpiritBox
                    }
                },
                new Ghost
                {
                    GhostType = EGhost.Deildegast,
                    Evidences = new List<EEvidence>
                    {
                        EEvidence.EMF5,
                        EEvidence.GhostWriting,
                        EEvidence.DOTS
                    }
                }
            ];

            _ghostsBackup = JsonConvert.DeserializeObject<List<Ghost>?>(JsonConvert.SerializeObject(_ghosts));
        }

        private void LoadKeys()
        {
            _keys.Add(Key.Q, 10);
            _keys.Add(Key.W, 11);
            _keys.Add(Key.E, 12);
            _keys.Add(Key.R, 13);
            _keys.Add(Key.T, 14);
            _keys.Add(Key.Y, 15);
            _keys.Add(Key.I, 16);
            _keys.Add(Key.O, 17);
            
            _keys.Add(Key.A, 18);
            _keys.Add(Key.S, 19);
            _keys.Add(Key.D, 20);
            _keys.Add(Key.F, 21);
            _keys.Add(Key.G, 22);
            _keys.Add(Key.H, 23);
            _keys.Add(Key.J, 24);
            
            _keys.Add(Key.X, 25);
            _keys.Add(Key.C, 26);
            _keys.Add(Key.V, 27);
            _keys.Add(Key.B, 28);
            _keys.Add(Key.N, 29);
            _keys.Add(Key.M, 30);
        }

        private static Key GetKey(int id) => _keys.Keys.FirstOrDefault(p => _keys[p] == id);

        private static string GetWord(int id)
        {
            if (_language != null && _language.Length > id)
            {
                return _language[id];
            }
            return "?";
        }

        private void OnGlobalKeyPressed(Key key)
        {
            if (key == _specialKey || key == _shortcutKet) return;
            if ((_isTemporarilyHidden || !_isFocused) && key != Key.F4) return;
            Dispatcher.Invoke(() =>
            {
                if (key == Key.F10)
                {
                    F10();
                }
                else if (_isVisible)
                {
                    switch (key)
                    {
                        case Key.F1: F1(); break;
                        case Key.F2: F2(); break;
                        case Key.F4: F4(); break;
#if DEBUG
                        case Key.F8: F5(); break;
#else
                        case Key.F5: F5(); break;
#endif
                        case Key.F6: F6(); break;
                        case Key.F7: F7(); break;
                        case Key.Delete: Delete(); break;
                        case Key.Subtract: Minus(); break;
                        case Key.Add: Plus(); break;
                        default:
                            {
                                var n = key - Key.D0;
                                if (_keys.ContainsKey(key))
                                {
                                    n = _keys[key];
                                }
                                if (n == 0 || key == Key.Oem3)
                                {
                                    if (!_evidences.Where(p => p.Value).Select(p => p.Key).Any())
                                    {
                                        for (var i = 0; i < _ghostsBackup!.Count; ++i)
                                        {
                                            _ghosts![i].Hidden = _ghostsBackup[i].Hidden;
                                        }
                                        foreach (var evidence in _evidencesBackup.Keys)
                                        {
                                            _evidences[evidence] = _evidencesBackup[evidence];
                                        }
                                        OnEvidencesUpdated();
                                    }
                                    else
                                    {
                                        for (var i = 0; i < _ghosts!.Count; ++i)
                                        {
                                            _ghostsBackup![i].Hidden = _ghosts[i].Hidden;
                                            _ghosts[i].Hidden = false;
                                        }
                                        foreach (var evidence in _evidences.Keys)
                                        {
                                            _evidencesBackup[evidence] = _evidences[evidence];
                                            _evidences[evidence] = false;
                                        }
                                        OnEvidencesUpdated();
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        if (GlobalInputHook.LCtrl && n > 0)
                                        {
                                            if (n < 10)
                                            {
                                                n = n - 1;
                                                if (_ghosts!.Count > n)
                                                {
                                                    _ghosts![n].Hidden = !_ghosts[n].Hidden;
                                                }
                                            }
                                            else
                                            {
                                                n = n - 1;
                                                if (_ghosts!.Count > n)
                                                {
                                                    _ghosts[n].Hidden = !_ghosts[n].Hidden;
                                                }
                                            }
                                            OnEvidencesUpdated();
                                        }
                                        else if (GlobalInputHook.LShift && n > 0 && n < 8)
                                        {
                                            n = n - 1;
                                            if (_ghosts!.Count > n)
                                            {
                                                _evidences[(EEvidence)n] = !_evidences[(EEvidence)n];
                                            }
                                            OnEvidencesUpdated();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                    }
                                }
                                break;
                            }
                    }
                }
            });
        }

        private void OnMouseButtonChanged(System.Windows.Input.MouseButton button, bool isDown, GlobalInputHook.POINT pt)
        {
            if (isDown) return;
            switch (button)
            {
                case MouseButton.XButton1:
                    {
                        _sw2.Reset();
                        F1();
                        break;
                    }
                case MouseButton.XButton2:
                    {
                        F1(_elapsedSw);
                        break;
                    }
            }
        }

        private void LaunchTheGame()
        {
            try
            {
                var proc = Process.GetProcessesByName(TargetProcessName).FirstOrDefault();
                if (proc == null)
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "steam://rungameid/739630",
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _keyboardHook.Dispose();
            base.OnClosed(e);
        }

        private void F10()
        {
            if (_isVisible)
            {
                Hide();
            }
            else
            {
                Show();
            }
            _isVisible = !_isVisible;
        }

        private void F1() => F1(TimeSpan.Zero);

        private void F1(TimeSpan elapsedSw)
        {
            if (_isTimerReset)
            {
                _isTimerReset = false;
                _assistTimerToken?.Cancel();
                _assistTimerToken = new CancellationTokenSource();
                tbAssistTimer.FontSize = _timerFontSizeInitial;
                tbAssistTimer.Foreground = Brushes.White;
                Task.Run(AssistTimer);
                _sw.Start(elapsedSw + _sw2.Elapsed);
                _sw2.Reset();
                tbAssistTimer.Visibility = Visibility.Visible;
                if (_swLastTimer.Elapsed.Seconds < 1)
                {
                    tbLastTimer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    tbLastTimer.Visibility = Visibility.Visible;
                }
            }
            else
            {
                _swLastTimer = new OffsetStopwatch(_sw.Elapsed);
                if (_swLastTimer.Elapsed.Seconds < 1)
                {
                    tbLastTimer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    tbLastTimer.Visibility = Visibility.Visible;

                    if (_swLastTimer.Elapsed.Minutes > 0)
                    {
                        tbLastTimer.Text = _swLastTimer.Elapsed.ToString("mm") + ":" + _swLastTimer.Elapsed.ToString("ss");
                    }
                    else
                    {
                        tbLastTimer.Text = _swLastTimer.Elapsed.ToString("ss");
                    }
                }
                _elapsedSw = _sw.Elapsed;
                _sw.Reset();
                _sw2.Start();
                _isTimerReset = true;
                tbAssistTimer.Visibility = Visibility.Collapsed;
            }
        }

        private void F2()
        {
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var languagesDir = Path.Combine(root!, "Languages");
            if (Directory.Exists(languagesDir))
            {
                var files = Directory.GetFiles(languagesDir, "*.txt", SearchOption.TopDirectoryOnly).ToList();
                if (files != null && files.Count > 1)
                {
                    var current = string.IsNullOrWhiteSpace(_languageFileName)
                        ? 0
                        : files.IndexOf(_languageFileName);
                    var next = current + 1;
                    if (next >= files.Count)
                    {
                        next = 0;
                    }
                    LoadLanguage(files[next]);
                    OnEvidencesUpdated();
                }
            }
        }

        private void F4()
        {
            _blinkTextToken?.Cancel();
            Application.Current.Shutdown();
        }

        private void F5()
        {
            var visible = About.Visibility == Visibility.Visible;
            if (visible)
            {
                About.Visibility = Visibility.Collapsed;
                FocusPhasmaphobia();
            }
            else
            {
                About.Visibility = Visibility.Visible;
                Randomizer.Visibility = Visibility.Collapsed;
                Statistics.Visibility = Visibility.Collapsed;
                Activate();
                Focus();
            }
        }

        private void F6()
        {
            var visible = Randomizer.Visibility == Visibility.Visible;
            if (visible)
            {
                Randomizer.Visibility = Visibility.Collapsed;
                FocusPhasmaphobia();
            }
            else
            {
                Randomizer.Visibility = Visibility.Visible;
                About.Visibility = Visibility.Collapsed;
                Statistics.Visibility = Visibility.Collapsed;
                Activate();
                Focus();
            }
        }

        private void F7()
        {
            var visible = Statistics.Visibility == Visibility.Visible;
            if (visible)
            {
                Statistics.Visibility = Visibility.Collapsed;
                FocusPhasmaphobia();
            }
            else
            {
                Statistics.Visibility = Visibility.Visible;
                About.Visibility = Visibility.Collapsed;
                Randomizer.Visibility = Visibility.Collapsed;
                Activate();
                Focus();
            }
        }

        private void Delete()
        {
            if (_isGameTimerReset)
            {
                _isGameTimerReset = false;
                _statistic.Reset();
                _statistic.Huntings = 0;
                _statistic.GhostEvents = 0;
                _statistic.ShadeGhostEvents = 0;
                tbHuntings.Text = "0";
                tbGhostEvents.Text = "0";
                tbShadeGhostEvents.Text = "0";
                tbSingingGhostEvents.Text = "0";
                cbChangingLocation.IsChecked = false;
                cbFuseBoxOff.IsChecked = false;
                cbMistForm.IsChecked = false;
                _gameTimerToken?.Cancel();
                _gameTimerToken = new CancellationTokenSource();
                Task.Run(GameTimer);
                _swGame.Start();
                tbGameTime.Visibility = Visibility.Visible;
            }
            else
            {
                _swGame.Reset();
                _isGameTimerReset = true;
                tbGameTime.Visibility = Visibility.Collapsed;
            }
        }

        private void Minus() => AddHuntingButton_Click(null, null);

        private void Plus() => AddGhostEventButton_Click(null, null);

        private async Task AssistTimer()
        {
            while (!(_assistTimerToken == null || _assistTimerToken.IsCancellationRequested))
            {
                Dispatcher.Invoke(() =>
                {
                    if (_sw.Elapsed.Minutes > 0)
                    {
                        tbAssistTimer.Text = _sw.Elapsed.ToString("mm") + ":" + _sw.Elapsed.ToString("ss");
                    }
                    else
                    {
                        tbAssistTimer.Text = _sw.Elapsed.ToString("ss");
                    }

                    if (_sw.Elapsed.Seconds >= 20 && _sw.Elapsed.Seconds < 22)
                    {
                        tbAssistTimer.FontSize = _timerFontSizeAbove20;
                    }
                    if (_sw.Elapsed.Seconds >= 25 && _sw.Elapsed.Seconds < 27)
                    {
                        tbAssistTimer.FontSize = _timerFontSizeAbove20 + 4;
                        tbAssistTimer.Foreground = Brushes.Red;
                    }

                    if (_sw.Elapsed.Seconds >= 100 && _sw.Elapsed.Seconds < 102)
                    {
                        tbAssistTimer.Foreground = Brushes.White;
                        tbAssistTimer.FontSize = _timerFontSizeAbove20;
                    }
                    if (_sw.Elapsed.Seconds >= 180 && _sw.Elapsed.Seconds < 182)
                    {
                        tbAssistTimer.FontSize = _timerFontSizeAbove20 + 4;
                        tbAssistTimer.Foreground = Brushes.Red;
                    }

                    if (_sw.Elapsed.TotalMinutes >= 10)
                    {
                        _assistTimerToken.Cancel();
                    }
                });
                await Task.Delay(100);
            }
        }

        private async Task GameTimer()
        {
            while (!(_gameTimerToken == null || _gameTimerToken.IsCancellationRequested))
            {
                Dispatcher.Invoke(() =>
                {
                    if (_swGame.Elapsed.Minutes > 0)
                    {
                        tbGameTime.Text = _swGame.Elapsed.ToString("mm") + ":" + _swGame.Elapsed.ToString("ss");
                    }
                    else
                    {
                        tbGameTime.Text = _swGame.Elapsed.ToString("ss");
                    }
                });
                await Task.Delay(100);
            }
        }

        private void OnEvidencesUpdated()
        {
            if (!_evidences.Where(p => p.Value).Select(p => p.Key).Any())
            {
                tbEvidences.Text = string.Empty;
                wpGhosts.Children.Clear();
                tbGhostsHidden.Text = string.Empty;
                tbEvidences.Visibility = Visibility.Collapsed;
                wpGhosts.Visibility = Visibility.Collapsed;
                return;
            }

            var evidences = new List<string>();
            foreach (var evidence in _evidences.Where(p => p.Value).Select(p => p.Key))
            {
                evidences.Add(Translate(evidence));
            }
            tbEvidences.Text = string.Join(" - ", evidences);
            tbEvidences.Visibility = string.IsNullOrWhiteSpace(tbEvidences.Text) ? Visibility.Collapsed : Visibility.Visible;

            var filter = new List<Ghost>();
            var filterHidden = new List<Ghost>();
            foreach (var ghost in _ghosts!)
            {
                if (_statistic.ChangedLocations && ghost.GhostType == EGhost.Goryo)
                {
                    filterHidden.Add(ghost);
                    continue;
                }

                if (_statistic.FuseBoxTurnedOff && ghost.GhostType == EGhost.Jinn)
                {
                    filterHidden.Add(ghost);
                    continue;
                }

                if (_statistic.MistForm && ghost.GhostType == EGhost.Oni)
                {
                    filterHidden.Add(ghost);
                    continue;
                }

                if (_statistic.SingingGhostEvent > 0 && ghost.GhostType == EGhost.Shade)
                {
                    filterHidden.Add(ghost);
                    continue;
                }

                if (ghost.Check(_evidences))
                {
                    if (ghost.Hidden)
                    {
                        filterHidden.Add(ghost);
                    }
                    else
                    {
                        filter.Add(ghost);
                    }
                }
            }
            if (filter.Any() || filterHidden.Any())
            {
                var ghosts = new List<string>();
                var ghostsDetails = new List<GhostDetails>();
                foreach (var ghost in filter)
                {
                    ghosts.Add(Translate(ghost.GhostType));
                    var gd = new GhostDetails(ghost);
                    gd.FontSize = _ghostsFontSize;
                    ghostsDetails.Add(gd);
                }
                var ghostsHidden = new List<string>();
                foreach (var ghost in filterHidden)
                {
                    ghostsHidden.Add(Translate(ghost.GhostType));
                }
                var left = string.Join (" - ", ghosts);
                var right = string.Join (" - ", ghostsHidden);
                tbGhostsHidden.Text = right;
                if (ghostsDetails.Count == 1)
                {
                    ghostsDetails.First().FontSize = _ghostsFontSizeIdentified;
                }
                else
                {
                    ghostsDetails.ForEach(p => p.FontSize = _ghostsFontSize);
                }

                    wpGhosts.Children.Clear();
                for (var i = 0; i < ghostsDetails.Count; ++i)
                {
                    if (i >= ghostsDetails.Count - 1)
                    {
                        ghostsDetails[i].ShowDash = false;
                    }
                    wpGhosts.Children.Add(ghostsDetails[i]);
                }
            }
            else
            {
                wpGhosts.Children.Clear();
            }
            wpGhosts.Visibility = wpGhosts.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string Translate(EEvidence evidence) => $"{(int)evidence + 1}. " + GetWord((int)evidence);

        public static string Translate(EGhost ghost)
        {
            var id = string.Empty;
            var n = (int)ghost;
            if (n >= 9)
            {
                id = GetKey(n + 1).ToString();
            }
            else
            {
                id = (n + 1).ToString();
            }
            return $"{id}. " + GetWord((int)ghost + 7);
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink lnk)
            {
                var url = lnk.NavigateUri.AbsoluteUri;
                if (string.IsNullOrEmpty(url)) return;
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }

        private void Image_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Image img)
            {
                var url = img.Tag?.ToString();
                if (string.IsNullOrEmpty(url)) return;
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
        }

        private async Task BlinkText()
        {
            while (!(_blinkTextToken == null || _blinkTextToken.IsCancellationRequested))
            {
                Dispatcher.Invoke(() =>
                {
                    tbBlink1.Visibility = tbBlink1.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                    tbBlink2.Visibility = tbBlink2.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                    tbBlink3.Visibility = tbBlink3.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                    tbBlink4.Visibility = tbBlink4.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                });
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        private void CheckVisibilityOnTimerTick(object? sender, EventArgs e)
        {
            var proc = Process.GetProcessesByName(TargetProcessName).FirstOrDefault();

            if (!_isTemporarilyHidden && proc == null)
            {
                _isTemporarilyHidden = true;
                if (HideWhenNotActive)
                {
                    Hide();
                }
            }
            else if (_isTemporarilyHidden && _isVisible && proc != null)
            {
                if (_isFocused)
                {
                    Show();
                }
                _isTemporarilyHidden = false;
            }
        }

        private void CheckFocusOnTimerTick(object? sender, EventArgs e)
        {
            if (_isTemporarilyHidden) return;

            var assistProcessName = Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().GetName().Name);
            var proc1Focused = IsAppFocused(assistProcessName!);
            var proc2Focused = IsAppFocused(TargetProcessName);

            if (!proc1Focused && !proc2Focused)
            {
                if (HideWhenNotActive)
                {
                    Hide();
                }
                _isFocused = false;
            }
            else
            {
                if (!_isFocused && _isVisible && !_isTemporarilyHidden && proc2Focused) // check the _isTemporarilyHidden one more time because it could be changed asynchronously
                {
                    Show();
                    _isFocused = true;
                }
            }
        }

        private void FocusPhasmaphobia()
        {
            var proc = Process.GetProcessesByName(TargetProcessName).FirstOrDefault();
            if (proc != null)
            {
                var hWnd = proc.MainWindowHandle;

                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }

                SetForegroundWindow(hWnd);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            // Remove APPWINDOW, add TOOLWINDOW → hides from Alt+Tab
            exStyle &= ~WS_EX_APPWINDOW;
            exStyle |= WS_EX_TOOLWINDOW;

            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }

        private static bool IsAppFocused(string processName)
        {
            var foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
            {
                return false;
            }

            GetWindowThreadProcessId(foreground, out uint pid);
            var proc = Process.GetProcessesByName(processName).FirstOrDefault();

            if (proc == null)
            {
                return false;
            }

            return proc.Id == pid;
        }

        #region DLL IMPORT
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        const int SW_RESTORE = 9;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;
        #endregion

        private void MapSizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn ||
                string.IsNullOrWhiteSpace(btn.Tag?.ToString()))
            {
                return;
            }

            var split = btn.Tag.ToString()!.Split(',');
            
            var tags = new List<byte>();
            foreach (var item in split)
            {
                byte.TryParse(item, out var value);
                tags.Add(value);
            }

            var items = new List<ListBoxItem>();
            var enabled = false;

            foreach (var child in MapsList.Items)
            {
                if (child is not ListBoxItem item)
                {
                    continue;
                }

                item.Foreground = Brushes.Black;

                if (string.IsNullOrWhiteSpace(item.Tag?.ToString()) ||
                    !byte.TryParse(item.Tag.ToString(), out var tag) ||
                    !tags.Contains(tag))
                {
                    continue;
                }

                items.Add(item);
                if (item.IsSelected)
                {
                    enabled = true;
                }
            }

            if (items.Any())
            {
                items.ForEach(p => p.IsSelected = !enabled);
            }
        }

        private CheckBox[]? _cbs = null;
        private TextBlock[]? _tbs = null;

        private void RndButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cbs == null)
            {
                _cbs = new[]
                {
                    cb0, cb1, cb2, cb3, cb4, cb5, cb6, cb7, cb8, cb9, cb10, cb11, cb12, cb13, cb14, cb15, cb16, cb17, cb18, cb19, cb20
                };
            }

            if (_tbs == null)
            {
                _tbs = new[]
                {
                    tb0, tb1, tb2, tb3, tb4, tb5, tb6, tb7, tb8, tb9, tb10, tb11, tb12, tb13, tb14, tb15, tb16, tb17, tb18, tb19, tb20
                };
            }

            var items = new List<ListBoxItem>();
            foreach (var child in MapsList.Items)
            {
                if (child is not ListBoxItem item)
                {
                    continue;
                }

                item.Foreground = Brushes.Black;

                if (!item.IsSelected)
                {
                    continue;
                }

                items.Add(item);
            }

            var rnd = new Random(DateTime.Now.Millisecond);

            if (items.Count > 0)
            {
                var max = items.Count;
                var mapId = rnd.Next(0, max);
                items[mapId].Foreground = Brushes.Green;
            }

            for (var i = 0; i < _cbs.Length; ++i)
            {
                var isChecked = rnd.Next(0, 2) == 1;
                _cbs[i].IsChecked = isChecked;
                
                _tbs[i].Text = isChecked
                    ? rnd.Next(1, 4).ToString()
                    : string.Empty;
            }

            if (!(cb17.IsChecked.HasValue && cb17.IsChecked.Value) && ((cb8.IsChecked.HasValue && cb8.IsChecked.Value) || (cb18.IsChecked.HasValue && cb18.IsChecked.Value)))
            {
                cb17.IsChecked = true;
                tb17.Text = rnd.Next(1, 4).ToString();
            }

            if (!(cb7.IsChecked.HasValue && cb7.IsChecked.Value))
            {
                cb16.IsChecked = false;
                tb16.Text = string.Empty;
            }
        }

        private void RemoveHuntingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_statistic.Huntings > 0)
            {
                --_statistic.Huntings;
                tbHuntings.Text = _statistic.Huntings.ToString();
            }
        }

        private void AddHuntingButton_Click(object sender, RoutedEventArgs e)
        {
            ++_statistic.Huntings;
            tbHuntings.Text = _statistic.Huntings.ToString();
        }

        private void RemoveGhostEventButton_Click(object sender, RoutedEventArgs e)
        {
            if (_statistic.GhostEvents > 0)
            {
                --_statistic.GhostEvents;
                tbGhostEvents.Text = _statistic.GhostEvents.ToString();
            }
        }

        private void AddGhostEventButton_Click(object sender, RoutedEventArgs e)
        {
            ++_statistic.GhostEvents;
            tbGhostEvents.Text = _statistic.GhostEvents.ToString();
        }

        private void RemoveShadeGhostEventButton_Click(object sender, RoutedEventArgs e)
        {
            if (_statistic.ShadeGhostEvents > 0)
            {
                --_statistic.ShadeGhostEvents;
                tbShadeGhostEvents.Text = _statistic.ShadeGhostEvents.ToString();
            }
        }

        private void AddShadeGhostEventButton_Click(object sender, RoutedEventArgs e)
        {
            ++_statistic.ShadeGhostEvents;
            tbShadeGhostEvents.Text = _statistic.ShadeGhostEvents.ToString();
        }

        private void RemoveSingingGhostEventButton_Click(object sender, RoutedEventArgs e)
        {
            if (_statistic.SingingGhostEvent > 0)
            {
                --_statistic.SingingGhostEvent;
                tbSingingGhostEvents.Text = _statistic.SingingGhostEvent.ToString();
                OnEvidencesUpdated();
            }
        }

        private void AddSingingGhostEventButton_Click(object sender, RoutedEventArgs e)
        {
            ++_statistic.SingingGhostEvent;
            tbSingingGhostEvents.Text = _statistic.SingingGhostEvent.ToString();
            OnEvidencesUpdated();
        }

        private void cbChangingLocationChecked(object sender, RoutedEventArgs e)
        {
            _statistic.ChangedLocations = !_statistic.ChangedLocations;
            OnEvidencesUpdated();
        }

        private void cbFuseBoxChecked(object sender, RoutedEventArgs e)
        {
            _statistic.FuseBoxTurnedOff = !_statistic.FuseBoxTurnedOff;
            OnEvidencesUpdated();
        }

        private void cbMistFormChecked(object sender, RoutedEventArgs e)
        {
            _statistic.MistForm = !_statistic.MistForm;
            OnEvidencesUpdated();
        }
    }
}