using Meadow;
using Meadow.Foundation.ICs.ADC;
using Meadow.Foundation.Sensors.Atmospheric;
using Meadow.Foundation.Sensors.Environmental;
using Meadow.Foundation.Sensors.Motion;

namespace LIMSMarlin;

internal class SensorService
{
    public event EventHandler<SensorData>? DataReady;

    private readonly Bmi270? _imu;
    private readonly Aht10? _tempSensor;
    private readonly Ens160? _airQualitySensor;
    private readonly Ads1115? _adc;

    public SensorService(RaspberryPi hardware)
    {
        var i2c = hardware.CreateI2cBus();

        try
        {
            _imu = new Bmi270(i2c, 0x68);
            Resolver.Log.Info("IMU found");
        }
        catch (Exception ex)
        {
            Resolver.Log.Error($"Failed to initialize BMI270: {ex.Message}");
            _imu = null;
        }

        try
        {
            _tempSensor = new Aht10(i2c, 0x38);
            _tempSensor.Read().GetAwaiter().GetResult(); // Initial read to see if the sensor is even there
            Resolver.Log.Info("Temp sensor found");

            // TODO: add calibration offset for temp
        }
        catch (Exception ex)
        {
            Resolver.Log.Error($"Failed to initialize AHT10: {ex.Message}");
            _tempSensor = null;
        }

        try
        {
            _airQualitySensor = new Ens160(i2c, 0x52);
            Resolver.Log.Info("Air quality sensor found");
        }
        catch (Exception ex)
        {
            Resolver.Log.Error($"Failed to initialize ENS160 at address 0x52: {ex.Message}");
            _airQualitySensor = null;
        }

        try
        {
            _adc = new Ads1115(
                i2c,
                Ads1x15Base.Addresses.Address_0x48,
                Ads1x15Base.MeasureMode.Continuous,
                Ads1x15Base.ChannelSetting.A0SingleEnded,
                Ads1115.SampleRateSetting.Sps128);

            Resolver.Log.Info("ADC found");

            // TODO: configure ADC range

        }
        catch (Exception ex)
        {
            Resolver.Log.Error($"Failed to initialize ADS1115: {ex.Message}");
            _adc = null;
        }
    }

    public async Task Start()
    {
        CancellationTokenSource cts = new CancellationTokenSource();
        Resolver.Log.Info("Starting sensor read thread...");
        await Task.Run(() => SensorReadProc(cts.Token), cts.Token);
    }

    private async Task SensorReadProc(CancellationToken cancellationToken)
    {
        long i = 0;
        var data = new SensorData();
        bool hasData = false;

        // TODO: make periods configurable
        while (!cancellationToken.IsCancellationRequested)
        {
            data.Clear();
            hasData = false;

            i++;

            if (_imu != null)
            {
                var reading = await _imu.Read();
                if (reading.Acceleration3D != null)
                {
                    data.AccelerationX = reading.Acceleration3D.Value.X.Gravity;
                    data.AccelerationY = reading.Acceleration3D.Value.Y.Gravity;
                    data.AccelerationZ = reading.Acceleration3D.Value.Z.Gravity;
                    hasData = true;
                }
            }

            if (i % 100 == 0)
            {
                if (_tempSensor != null)
                {
                    var t = await _tempSensor.Read();
                    if (t.Temperature != null)
                    {
                        data.Temperature = t.Temperature.Value.Celsius;
                        hasData = true;
                    }
                    if (t.Humidity != null)
                    {
                        data.Humidity = t.Humidity.Value.Percent;
                        hasData = true;
                    }
                }

                if (_airQualitySensor != null)
                {
                    var aq = await _airQualitySensor.Read();
                    if (aq.TVOCConcentration != null)
                    {
                        data.TVOC = aq.TVOCConcentration.Value.PartsPerMillion;
                        hasData = true;
                    }
                    if (aq.CO2Concentration != null)
                    {
                        data.CO2 = aq.CO2Concentration.Value.PartsPerMillion;
                        hasData = true;
                    }
                }

                if (_adc != null)
                {
                    var channel0 = await _adc.Read();
                    data.ADCValue = channel0.Volts;
                    hasData = true;
                }
            }

            if (hasData)
            {
                DataReady?.Invoke(this, data);
            }

            await Task.Delay(10); // 100Hz
        }
    }
}
