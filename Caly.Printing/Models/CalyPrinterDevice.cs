namespace Caly.Printing.Models
{
    public sealed class CalyPrinterDevice
    {
        public required string Name { get; init; }
        
        public bool IsOnline { get; set; }
        
        public string? Datatype { get; init; }

        public required string PortName { get; init; }

        public string? ServerName { get; init; }

        public required string DriverName { get; init; }
    }
}
