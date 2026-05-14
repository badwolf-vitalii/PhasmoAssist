using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using Enum;

namespace PhasmoAssist
{
    /// <summary>
    /// Interaction logic for GhostDetails.xaml
    /// </summary>
    public partial class GhostDetails : UserControl
    {
        private GhostDetailsViewModel _vm { get; set; }

        private EGhost _ghostType;
        public EGhost GhostType
        {
            get => _ghostType;
            set
            {
                _ghostType = value;
                DrawSanity();
            }
        }

        public GhostDetails()
        {
            InitializeComponent();
            _vm = new GhostDetailsViewModel();
            DataContext = _vm;
            _vm.FontSize = FontSize;
        }

        public GhostDetails(GhostDetailsViewModel viewModel, bool showDash = true)
        {
            InitializeComponent();
            _vm = viewModel;
            DataContext = _vm;
            ShowDash = showDash;
            _vm.FontSize = FontSize;
        }

        public GhostDetails(Ghost ghost, bool showDash = true)
        {
            InitializeComponent();
            _vm = new GhostDetailsViewModel();
            DataContext = _vm;
            Name = MainWindow.Translate(ghost.GhostType);
            ShowDash = showDash;
            _vm.FontSize = FontSize;
            GhostType = ghost.GhostType;
        }

        public string Name
        {
            get => _vm.Name;
            set => _vm.Name = value;
        }

        public bool ShowDash
        {
            get => _vm.ShowDash;
            set => _vm.ShowDash = value;
        }

        public new double FontSize
        {
            get => _vm.FontSize;
            set => _vm.FontSize = value;
        }

        private void DrawSanity()
        {
            switch (GhostType)
            {
                case EGhost.Dayan: _vm.Sanity = "45🧍 / 65🏃 / 50↔️"; break;
                case EGhost.Mare: _vm.Sanity = "60🔆 / 40☀️"; break;
                case EGhost.Onryo: _vm.Sanity = "60"; break;
                case EGhost.Raiju: _vm.Sanity = "50 / 65⚡"; break;
                case EGhost.Yokai: _vm.Sanity = "50 / 80🔊"; break;
                case EGhost.Thaye: _vm.Sanity = "75 - 15"; break;
                case EGhost.Gallu: _vm.Sanity = "40🔻/ 50 / 60🔺"; break;
                case EGhost.Obambo: _vm.Sanity = "10🔻/ 60🔺"; break;
                case EGhost.Shade: _vm.Sanity = "35"; break;
                case EGhost.Deogen: _vm.Sanity = "40"; break;
                case EGhost.Kormos: _vm.Sanity = "50🧍 / 70🏃"; break;
                default: _vm.Sanity = "50"; break;
            }
        }
    }

    public class GhostDetailsViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                RaisePropertyChanged();
            }
        }

        private string? _sanity;
        public string? Sanity
        {
            get => _sanity;
            set
            {
                _sanity = value;
                RaisePropertyChanged();
                RaisePropertyChanged("SanityVisibility");
            }
        }

        private bool _showDash = true;
        public bool ShowDash
        {
            get => _showDash;
            set
            {
                _showDash = value;
                RaisePropertyChanged();
            }
        }

        private double _fontSize = 25;
        public double FontSize
        {
            get => _fontSize;
            set
            {
                _fontSize = value;
                RaisePropertyChanged();
            }
        }

        public Visibility SanityVisibility => string.IsNullOrWhiteSpace(Sanity) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility DashVisibility => _showDash ? Visibility.Visible : Visibility.Collapsed;

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            AssertPropertyExists(propertyName);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected void NotifyPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Warns the developer if this object does not have a public property with the specified name.
        /// This method does not exist in a Release build.
        /// </summary>
        [Conditional("DEBUG")]
        [DebuggerStepThrough]
        public virtual void AssertPropertyExists(string propertyName)
        {
            // Verify that the property name matches a real, public, instance property on this object.
            var properties = TypeDescriptor.GetProperties(this);
            if (properties[propertyName] == null)
                Debug.Fail("Invalid property name: {propertyName}");
        }
        #endregion
    }
}
