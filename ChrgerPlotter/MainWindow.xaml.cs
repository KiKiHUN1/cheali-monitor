using ScottPlot;
using ScottPlot.AxisPanels;
using ScottPlot.Plottables;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ChrgerPlotter
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        enum ProgramType
        {
            Charge, ChargeBalance, Balance, Discharge, FastCharge,
            Storage, StorageBalance, DischargeChargeCycle, CapacityCheck,
            EditBattery,Calibrate, LAST_PROGRAM_TYPE, UNKNOWN
        };
        struct ChealiFrame
        {
            public int Index;
            public ProgramType State;
            public double Time;
            public int Voltage_mv;
            public int Current_mA;
            public int Capacity;
            public int Power_mW;
            public int Energy_mWh;
            public int TempExternal;
            public int TempInternal;
            public int InputVoltage_mV;
            public int Cell1, Cell2, Cell3, Cell4, Cell5, Cell6;
            public int R1, R2, R3, R4, R5, R6;
            public int Rbat;
            public int Rwire;
            public int Percent;
            public int ETA;
        }

        private string? _selectedPort;
        double prev_time = 0.0;

        System.IO.Ports.SerialPort serialPort;

        private DispatcherTimer timer;
        Task readTask;

        ScottPlot.Plottables.DataLogger Logger_Voltage;
        ScottPlot.Plottables.DataLogger Logger_Amperage;

        public MainWindow()
        {
            InitializeComponent();
        }

        bool OpenSerial()
        {
            bool ret = true;
            serialPort = new System.IO.Ports.SerialPort(_selectedPort, 57600);
            serialPort.NewLine = "\n";
            try
            {
                serialPort.Open();
            }
            catch
            {
                ret =  false;
            }
            
            return ret;
        }

        private void GeneratePlot()
        {
            Plot plt = wpfPlot.Plot; //get plot reference
            plt.DataBackground.Color = Color.FromHex("#8db5a8");
            plt.FigureBackground.Color = Color.FromHex("#8db5a8");
            plt.Clear();
            IYAxis left = wpfPlot.Plot.Axes.Left; //left Y-axis
            RightAxis right = wpfPlot.Plot.Axes.AddRightAxis(); //right Y-axis
            var bottom = wpfPlot.Plot.Axes.Bottom; //bottom X-axis
            wpfPlot.Plot.Axes.AutoScaleExpandX(bottom); // allow X-axis to expand as new data comes in

            // Add voltage on left Y-axis
            left.Label.Text = "Voltage (V)";
            Logger_Voltage = plt.Add.DataLogger();
            Logger_Voltage.Axes.YAxis = left;
            left.Min = 0; 
            left.Max = 30; 
            HorizontalLine hLineVolt = wpfPlot.Plot.Add.HorizontalLine(0, 1.1f, ScottPlot.Color.FromHex("#1f4fbf")); // Add a horizontal line at 0V for reference
            hLineVolt.Axes.YAxis = left;
            hLineVolt.Color = ScottPlot.Color.FromHex("#1f4fbf");

            // Add current on right Y-axis
            right.LabelText = "Current (A)";
            Logger_Amperage = plt.Add.DataLogger();
            Logger_Amperage .Axes.YAxis = right;

            // Set scale for current axis
            right.Min = 0;    // minimum current
            right.Max = 5;    // maximum current
            HorizontalLine hLineAmp = wpfPlot.Plot.Add.HorizontalLine(0, 1.1f); // Add a horizontal line at 0A for reference
            hLineAmp.Axes.YAxis = right;
            hLineAmp.Color = ScottPlot.Color.FromHex("#e87517");
            
            // Title and X-axis
            bottom.Label.Text = "Time (s)";
            bottom.Min = 0;

            wpfPlot.Refresh(); // Refresh the plot to display changes
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CB_Port.ItemsSource = System.IO.Ports.SerialPort.GetPortNames();
            GeneratePlot();
        }

        private void StartTimer()
        {
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(0.2); // 200ms interval
            timer.Tick += new EventHandler(Timer_Tick);
            timer.Start();
        }
        
        private async void Timer_Tick(object? sender, EventArgs e)
        {
            // Prevent overlapping reads: if a previous read is still running, skip this tick
            if (readTask != null && !readTask.IsCompleted)
                return;

            readTask = ReadDataAsync();
            await readTask;
        }

        async Task ReadDataAsync()
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                return;
            }

            string line;
            try
            {
                //run it on a background thread to avoid freezing the UI
                line = await Task.Run(() => serialPort.ReadLine());
            }
            catch
            {
                // If reading failed (port closed, IO error, etc.)
                return;
            }

            ChealiFrame parsedFrame;
            try
            {
                // returns a new frame
                parsedFrame = await Task.Run(() => ParseLine(line)); 
            }
            catch
            {
                return;
            }

            // If the charger is reseted
            if (prev_time > parsedFrame.Time)
            {
                Logger_Voltage.Clear();
                Logger_Amperage.Clear();
            }
            prev_time = parsedFrame.Time;

            Logger_Voltage.Add(new Coordinates(x: parsedFrame.Time, y: parsedFrame.Voltage_mv * 0.001));
            Logger_Amperage.Add(new Coordinates(x: parsedFrame.Time, y: parsedFrame.Current_mA * 0.001));

            TB_Voltage.Text = (parsedFrame.Voltage_mv * 0.001).ToString("0.000") + " V";
            TB_C1.Text = (parsedFrame.Cell1 * 0.001).ToString("0.000") + " V";
            TB_C2.Text = (parsedFrame.Cell2 * 0.001).ToString("0.000") + " V";
            TB_C3.Text = (parsedFrame.Cell3 * 0.001).ToString("0.000") + " V";
            TB_C4.Text = (parsedFrame.Cell4 * 0.001).ToString("0.000") + " V";
            TB_C5.Text = (parsedFrame.Cell5 * 0.001).ToString("0.000") + " V";
            TB_C6.Text = (parsedFrame.Cell6 * 0.001).ToString("0.000") + " V";
            TB_R1.Text = (parsedFrame.R1 * 0.001).ToString("0.000") + " Ω";
            TB_R2.Text = (parsedFrame.R2 * 0.001).ToString("0.000") + " Ω";
            TB_R3.Text = (parsedFrame.R3 * 0.001).ToString("0.000") + " Ω";
            TB_R4.Text = (parsedFrame.R4 * 0.001).ToString("0.000") + " Ω";
            TB_R5.Text = (parsedFrame.R5 * 0.001).ToString("0.000") + " Ω";
            TB_R6.Text = (parsedFrame.R6 * 0.001).ToString("0.000") + " Ω";
            TB_Rbat.Text = (parsedFrame.Rbat * 0.001).ToString("0.000") + " Ω";
            TB_Rwire.Text = (parsedFrame.Rwire * 0.001).ToString("0.000") + " Ω";
            TB_TempIn.Text = (parsedFrame.TempInternal * 0.01).ToString("0.000") + " °C";
            TB_TempOut.Text = (parsedFrame.TempExternal * 0.01).ToString("0.000") + " °C";
            TB_TimeEllapse.Text = parsedFrame.Time.ToString("0.0") + " s";
            TB_TimeRemaining.Text = parsedFrame.ETA.ToString("0.0") + " s";
            TB_Current.Text = (parsedFrame.Current_mA * 0.001).ToString("0.000") + " A";
            TB_Capacity.Text = parsedFrame.Capacity.ToString("0.0") + " mAh";
            TB_Energy.Text = parsedFrame.Energy_mWh.ToString("0.0") + " mWh";
            TB_Power.Text = parsedFrame.Power_mW.ToString("0.0") + " mW";
            TB_Percent.Text = parsedFrame.Percent.ToString("0") + " %";
            TB_Mode.Text = parsedFrame.State.ToString();
            wpfPlot.Refresh();
        }

        static ChealiFrame ParseLine(string line)
        {
            string[] parts = line.Split(';');
            ProgramType mode = ProgramType.UNKNOWN; // default value

            if (int.TryParse(parts[1], out int value) &&
                 Enum.IsDefined(typeof(ProgramType), value))
            {
                mode = (ProgramType)value;//Set mode
            }

            return new ChealiFrame
            {
                Index = int.Parse(parts[0].ToCharArray()[1].ToString()),
                State = mode, //parts[1] mode
                Time = double.Parse(parts[2], CultureInfo.InvariantCulture),
                Voltage_mv = int.Parse(parts[3]),
                Current_mA = int.Parse(parts[4]),
                Capacity = int.Parse(parts[5]),
                Power_mW = int.Parse(parts[6]),
                Energy_mWh = int.Parse(parts[7]),
                TempExternal = int.Parse(parts[8]),
                TempInternal = int.Parse(parts[9]),
                InputVoltage_mV = int.Parse(parts[10]),
                Cell1 = int.Parse(parts[11]),
                Cell2 = int.Parse(parts[12]),
                Cell3 = int.Parse(parts[13]),
                Cell4 = int.Parse(parts[14]),
                Cell5 = int.Parse(parts[15]),
                Cell6 = int.Parse(parts[16]),
                R1 = int.Parse(parts[17]),
                R2 = int.Parse(parts[18]),
                R3 = int.Parse(parts[19]),
                R4 = int.Parse(parts[20]),
                R5 = int.Parse(parts[21]),
                R6 = int.Parse(parts[22]),
                Rbat = int.Parse(parts[23]),
                Rwire = int.Parse(parts[24]),
                Percent = int.Parse(parts[25]),
                ETA = int.Parse(parts[26])
            };
        }

        private void CB_Port_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedPort = CB_Port.SelectedItem.ToString();
            if (!string.IsNullOrEmpty(_selectedPort))
            {
                BTN_Start.IsEnabled = true;
            }
            else
            {
                BTN_Start.IsEnabled = false;
            }
        }

        private void BTN_Start_Click(object sender, RoutedEventArgs e)
        {
            BTN_Start.IsEnabled = false;
            if(OpenSerial())
            {
                CB_Port.IsEnabled = false;
                BTN_Stop.IsEnabled = true;
                StartTimer();
            }
            else
            {
                MessageBox.Show("Failed to open serial port. Please check the connection and try again.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BTN_Start.IsEnabled = true;
            }
        }

        private async void BTN_Stop_Click(object sender, RoutedEventArgs e)
        {
            // Stop the timer
            timer.Stop();

            if (readTask != null && !readTask.IsCompleted)
            {
                try
                {
                    serialPort.Close();
                }
                catch
                {
                    // ignore
                }

                try
                {
                    await readTask;
                }
                catch
                {
                    // ignore any exceptions from the cancelled/aborted read
                }
            }
            else
            {
                try { serialPort.Close(); } catch { }
            }

            CB_Port.IsEnabled = true;
            BTN_Start.IsEnabled = true;
            BTN_Stop.IsEnabled = false;
        }
    }
}