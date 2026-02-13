using NodeNetwork.Toolkit.ValueNode;
using NodeNetwork.ViewModels;
using NodeNetwork.Views;
using ReactiveUI;
using Splat;
using System;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using DynamicData;
using NodeNetworkApp.ViewModels;
using YoutubeShortsClass.FormulaEvaluator;
using static YoutubeShortsClass.Helpers.GeneralEnums;
using NodeNetworkApp.Views;

namespace NodeNetworkApp.ViewModels.NodesFunctions
{
    public class RandomGeneratorSettingsNodeViewModel : MyNodeViewModelBase, IRunnableNode
    {
        private readonly BehaviorSubject<object> _outputSettingsSubject;
        private RandomGenerator.RandomGeneratorSettings _currentSettings;

        static RandomGeneratorSettingsNodeViewModel()
        {
            Locator.CurrentMutable.Register(() => new ColorizedNodeView<RandomGeneratorSettingsNodeViewModel>(), typeof(IViewFor<RandomGeneratorSettingsNodeViewModel>));
        }

        // Inputs
        public ValueNodeInputViewModel<int> MinIntInput { get; }
        public ValueNodeInputViewModel<int> MaxIntInput { get; }
        public ValueNodeInputViewModel<double> MinDoubleInput { get; }
        public ValueNodeInputViewModel<double> MaxDoubleInput { get; }
        public ValueNodeInputViewModel<TimeSpan> MinTimeSpanInput { get; }
        public ValueNodeInputViewModel<TimeSpan> MaxTimeSpanInput { get; }
        public ValueNodeInputViewModel<DateTime?> MinDateTimeInput { get; }
        public ValueNodeInputViewModel<DateTime?> MaxDateTimeInput { get; }
        public ValueNodeInputViewModel<int> StringLengthInput { get; }
        public ValueNodeInputViewModel<string> AllowedCharactersInput { get; }

        // Output
        public ValueNodeOutputViewModel<object> OutputSettings { get; }

        private string _workingDirectory;
        public override string WorkingDirectory => _workingDirectory;

        public RandomGeneratorSettingsNodeViewModel(string id = null) : base(id)
        {
            Name = "Random Generator Settings";
            _workingDirectory = Guid.NewGuid().ToString();
            _currentSettings = new RandomGenerator.RandomGeneratorSettings();
            _outputSettingsSubject = new BehaviorSubject<object>((object)_currentSettings);

            // Initialize Inputs
            MinIntInput = new ValueNodeInputViewModel<int>
            {
                Name = "Min Int",
                Editor = new IntegerValueEditorViewModel { Value = _currentSettings.MinInt }
            };
            MinIntInput.ValueChanged.Subscribe(v => { _currentSettings.MinInt = v; UpdateOutput(); });
            Inputs.Add(MinIntInput);

            MaxIntInput = new ValueNodeInputViewModel<int>
            {
                Name = "Max Int",
                Editor = new IntegerValueEditorViewModel { Value = _currentSettings.MaxInt }
            };
            MaxIntInput.ValueChanged.Subscribe(v => { _currentSettings.MaxInt = v; UpdateOutput(); });
            Inputs.Add(MaxIntInput);

            MinDoubleInput = new ValueNodeInputViewModel<double>
            {
                Name = "Min Double",
                Editor = new DoubleValueEditorViewModel { Value = _currentSettings.MinDouble }
            };
            MinDoubleInput.ValueChanged.Subscribe(v => { _currentSettings.MinDouble = v; UpdateOutput(); });
            Inputs.Add(MinDoubleInput);

            MaxDoubleInput = new ValueNodeInputViewModel<double>
            {
                Name = "Max Double",
                Editor = new DoubleValueEditorViewModel { Value = _currentSettings.MaxDouble }
            };
            MaxDoubleInput.ValueChanged.Subscribe(v => { _currentSettings.MaxDouble = v; UpdateOutput(); });
            Inputs.Add(MaxDoubleInput);

            MinTimeSpanInput = new ValueNodeInputViewModel<TimeSpan>
            {
                Name = "Min TimeSpan",
                Editor = new TimespanEditorViewModel { Value = _currentSettings.MinTimeSpan }
            };
            MinTimeSpanInput.ValueChanged.Subscribe(v => { _currentSettings.MinTimeSpan = v; UpdateOutput(); });
            Inputs.Add(MinTimeSpanInput);

            MaxTimeSpanInput = new ValueNodeInputViewModel<TimeSpan>
            {
                Name = "Max TimeSpan",
                Editor = new TimespanEditorViewModel { Value = _currentSettings.MaxTimeSpan }
            };
            MaxTimeSpanInput.ValueChanged.Subscribe(v => { _currentSettings.MaxTimeSpan = v; UpdateOutput(); });
            Inputs.Add(MaxTimeSpanInput);

            MinDateTimeInput = new ValueNodeInputViewModel<DateTime?>
            {
                Name = "Min DateTime",
                Editor = new DateTimeNodeEditorViewModel { SelectedDate = (DateTime)(DateTime?)_currentSettings.MinDateTime }
            };
            MinDateTimeInput.ValueChanged.Subscribe(v => { _currentSettings.MinDateTime = (DateTime)v; UpdateOutput(); });
            Inputs.Add(MinDateTimeInput);

            MaxDateTimeInput = new ValueNodeInputViewModel<DateTime?>
            {
                Name = "Max DateTime",
                Editor = new DateTimeNodeEditorViewModel { SelectedDate = (DateTime)(DateTime?)_currentSettings.MaxDateTime }
            };
            MaxDateTimeInput.ValueChanged.Subscribe(v => { _currentSettings.MaxDateTime = (DateTime)v; UpdateOutput(); });
            Inputs.Add(MaxDateTimeInput);

            StringLengthInput = new ValueNodeInputViewModel<int>
            {
                Name = "String Length",
                Editor = new IntegerValueEditorViewModel { Value = _currentSettings.StringLength }
            };
            StringLengthInput.ValueChanged.Subscribe(v => { _currentSettings.StringLength = v; UpdateOutput(); });
            Inputs.Add(StringLengthInput);

            AllowedCharactersInput = new ValueNodeInputViewModel<string>
            {
                Name = "Allowed Characters",
                Editor = new StringNodeEditorViewModel { Text = _currentSettings.AllowedCharacters }
            };
            AllowedCharactersInput.ValueChanged.Subscribe(v => { _currentSettings.AllowedCharacters = v; UpdateOutput(); });
            Inputs.Add(AllowedCharactersInput);

            // Initialize Output
            OutputSettings = new ValueNodeOutputViewModel<object>
            {
                Name = "Random Generator Settings",
                Value = _outputSettingsSubject
            };
            Outputs.Add(OutputSettings);

            RegisterMetadata(
                friendlyName: "Random Generator Settings",
                description: "Creates a RandomGeneratorSettings object for use with the Random Generator node.",
                categories: new[] { NodeCategory.Utility },
                tags: new[] { "random", "generator", "settings" },
                nodeData: new List<NodeDataInfo>()
                {
                    new NodeDataInfo { PortName = "Min Int",            DataFieldName = nameof(MinIntInput) },
                    new NodeDataInfo { PortName = "Max Int",            DataFieldName = nameof(MaxIntInput) },
                    new NodeDataInfo { PortName = "Min Double",         DataFieldName = nameof(MinDoubleInput) },
                    new NodeDataInfo { PortName = "Max Double",         DataFieldName = nameof(MaxDoubleInput) },
                    new NodeDataInfo { PortName = "Min TimeSpan",       DataFieldName = nameof(MinTimeSpanInput) },
                    new NodeDataInfo { PortName = "Max TimeSpan",       DataFieldName = nameof(MaxTimeSpanInput) },
                    new NodeDataInfo { PortName = "Min DateTime",       DataFieldName = nameof(MinDateTimeInput) },
                    new NodeDataInfo { PortName = "Max DateTime",       DataFieldName = nameof(MaxDateTimeInput) },
                    new NodeDataInfo { PortName = "String Length",      DataFieldName = nameof(StringLengthInput) },
                    new NodeDataInfo { PortName = "Allowed Characters", DataFieldName = nameof(AllowedCharactersInput) }
                }
            );
        }

        private void UpdateOutput()
        {
            var old = _currentSettings ?? new RandomGenerator.RandomGeneratorSettings();

            var newSettings = new RandomGenerator.RandomGeneratorSettings
            {
                MinInt = MinIntInput?.Value ?? old.MinInt,
                MaxInt = MaxIntInput?.Value ?? old.MaxInt,
                MinDouble = MinDoubleInput?.Value ?? old.MinDouble,
                MaxDouble = MaxDoubleInput?.Value ?? old.MaxDouble,
                MinTimeSpan = MinTimeSpanInput?.Value ?? old.MinTimeSpan,
                MaxTimeSpan = MaxTimeSpanInput?.Value ?? old.MaxTimeSpan,
                MinDateTime = MinDateTimeInput?.Value ?? old.MinDateTime,
                MaxDateTime = MaxDateTimeInput?.Value ?? old.MaxDateTime,
                StringLength = StringLengthInput?.Value ?? old.StringLength,
                AllowedCharacters = AllowedCharactersInput?.Value ?? old.AllowedCharacters
            };

            _currentSettings = newSettings;
            _outputSettingsSubject.OnNext(_currentSettings);
        }

        public Task Run()
        {
            UpdateOutput();
            return Task.CompletedTask;
        }
    }
}
