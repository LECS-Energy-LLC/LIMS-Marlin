namespace LIMSMarlin;

public class SensorData
{
    public double? AccelerationX { get; set; }
    public double? AccelerationY { get; set; }
    public double? AccelerationZ { get; set; }
    public double? Temperature { get; set; }
    public double? Humidity { get; set; }
    public double? TVOC { get; set; }
    public double? CO2 { get; set; }
    public double? ADCValue { get; set; }

    public void Clear()
    {
        AccelerationX = null;
        AccelerationY = null;
        AccelerationZ = null;
        Temperature = null;
        Humidity = null;
        TVOC = null;
        CO2 = null;
        ADCValue = null;
    }
}
