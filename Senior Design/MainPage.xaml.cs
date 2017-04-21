using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// Added additional Libraries:
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices;
using Windows.Devices.Gpio;
using Windows.Devices.Spi;
using Windows.Devices.Enumeration;

// PWM Library and Lightning Driver
using Microsoft.IoT.Lightning.Providers;
using Windows.Devices.Pwm;


namespace Senior_Design
{

    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            // Clean up the code before and after running
            Unloaded += MainPage_Unloaded;
          
            // Initialize the GPIO and SPI communication
            InitAll();

        }
        // --------------------------------------
        enum AdcDevice { MCP3008 };
        private AdcDevice ADC_DEVICE = AdcDevice.MCP3008;

        // Channel configuration for ADC converter (00000001 10000000 channel data)
        private readonly byte[] MCP3008_CONFIG = { 0x01, 0x80 };
        private Timer periodicTimer;
        private int adcValue;
        private const string SPI_CONTROLLER_NAME = "SPI0"; // Rasp Pi name and pin defined
        private const Int32 SPI_CHIP_SELECT_LINE = 0; // Line 0 maps to physcical pin 24 on rasp pi
        private SpiDevice SpiADC;

        // GPIO Pins to output PWM Signal
        private const int PWM_PIN = 4;
        private GpioPin pwmPin;

        private GpioPin _pin22;
        private PwmPin _pin27;

        //--------------------------
        // Initialize GPIO Function

        private void InitGpio()
        {
           
            var gpio = GpioController.GetDefault();
            LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();

            // Test if no gpio has been detected
            if (gpio == null)
            {
                throw new Exception("No GPIO detected");
            }
            // Establish pwmPin for GPIO output
            pwmPin = gpio.OpenPin(PWM_PIN);

            // Before writing out the PWM signal, set a default value
            pwmPin.Write(GpioPinValue.High);
            pwmPin.SetDriveMode(GpioPinDriveMode.Output);

        }

        private void PWMSignal()
        {
            LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();
            // Since we are using the MCP3008 ADC converter, Resolution = 1024
           // int adcResolution = 1024;
            // Current test output using GPIO pin 4, was able to trigger the result on the o-scope

            double fuckYou = adcValue;
            _pin27.SetActiveDutyCyclePercentage(fuckYou / 1024);
          
        }

        // Convert Byte to an Int
        public int convertToInt(byte[] data)
        {
            int result = 0;

            result = data[1] & 0x03;
            result <<= 8;
            result += data[2];

            return result;
        }

        public void ReadADC()
        {
            // Read and write buffers
            byte[] readBuffer = new byte[3]; // Buffer to hold read data
            byte[] writeBuffer = new byte[3] { 0x00, 0x00, 0x00 };

            writeBuffer[0] = MCP3008_CONFIG[0];
            writeBuffer[1] = MCP3008_CONFIG[1];

            // Read data from ADC and convert it to an int
            SpiADC.TransferFullDuplex(writeBuffer, readBuffer);
            adcValue = convertToInt(readBuffer);

            // Now update this value on the screen
            var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                // Update value in textbox
                double value = adcValue;
                PWM_Status.Text = (value/1024).ToString();
            });
        }

        private void MainPage_Unloaded(object sender, object args)
        {
            LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();
            if (SpiADC != null)
            {
                SpiADC.Dispose();
            }
            if (pwmPin != null)
            {
                pwmPin.Dispose();
            }
        }

        // Read from the ADC and update the UI to ouput the PWM
        private void Timer_Tick(object state)
        {
            // For every timer tick, call the above two functions
            ReadADC();
            PWMSignal();
        }

        // Initialize SPI Communication
        private async Task InitSPI()
        {
            LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();
            try
            {
                var settings = new SpiConnectionSettings(SPI_CHIP_SELECT_LINE);
                settings.ClockFrequency = 5000000; // Clock frequency = .5Mhz
                settings.Mode = SpiMode.Mode0; // Mode0 = idle lock clock polarity

                var controller = await SpiController.GetDefaultAsync();
                SpiADC = controller.GetDevice(settings);

                // I
                var pwmControllers = await PwmController.GetControllersAsync(LightningPwmProvider.GetPwmProvider());
                var pwmController = pwmControllers[1]; // the on-device controller
                pwmController.SetDesiredFrequency(50); // try to match 50Hz

                _pin27 = pwmController.OpenPin(27);
                _pin27.SetActiveDutyCyclePercentage(0);
                _pin27.Start();
                var gpioController = await GpioController.GetDefaultAsync();

                _pin22 = gpioController.OpenPin(22);
                _pin22.SetDriveMode(GpioPinDriveMode.Output);
                _pin22.Write(GpioPinValue.Low);


            }
            catch (Exception ex)
            {
                throw new Exception("SPI Initialization failed", ex);
            }
        }

        // Additonal Functions
        private async void InitAll()
        {
            // Utilizing the try catch to detect if any issues have been found
            LowLevelDevicesController.DefaultProvider = LightningProvider.GetAggregateProvider();
            try
            {
                InitGpio();
                await InitSPI();
            }
            catch (Exception ex)
            {
                Status.Text = ex.Message;
                return;
            }
            // Since everything is initialized, get a timer going to read data every 500ms

            periodicTimer = new Timer(this.Timer_Tick, null, 0, 100);
            Status.Text = "Status: Running";

        }

        private void textBlock_SelectionChanged(object sender, RoutedEventArgs e)
        {

        }
    }
}
