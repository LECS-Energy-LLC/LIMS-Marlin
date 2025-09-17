namespace LIMSMarlin.SDK;

public class SensorData
{
    public double? AccelerationX { get; set; }
    public double? AccelerationY { get; set; }
    public double? AccelerationZ { get; set; }
    public double? Temperature { get; set; }
    public double? Humidity { get; set; }
    public double? TVOC { get; set; }
    public double? CO2 { get; set; }
    public double? ADC0Value { get; set; }
    public double? ADC1Value { get; set; }
    public double? ADC2Value { get; set; }
    public double? ADC3Value { get; set; }

    public void Clear()
    {
        AccelerationX = null;
        AccelerationY = null;
        AccelerationZ = null;
        Temperature = null;
        Humidity = null;
        TVOC = null;
        CO2 = null;
        ADC0Value = null;
        ADC1Value = null;
        ADC2Value = null;
        ADC3Value = null;
    }
}
