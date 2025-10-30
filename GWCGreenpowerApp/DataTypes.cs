using System;
using System.Collections.Generic;

namespace GWCGreenpowerApp;
public class ResultEntry
{
    public string Name { get; set; } = "";
    public string Car { get; set; } = "";
    public float LapTime { get; set; } = 0f;
    public  string StartTime { get; set; } = "";
}

public class FileData
{
    public string? BluetoothConnected { get; set; }

    [CsvHelper.Configuration.Attributes.Name("DateTime (date)")]
    public string? DateTimeString { get; set; }

    [CsvHelper.Configuration.Attributes.Name("DateTime (ms)")]
    public string? Miliseconds { get; set; }

    [CsvHelper.Configuration.Attributes.Name("GPS latitude (°)")]
    public float? Latitude { get; set; }

    [CsvHelper.Configuration.Attributes.Name("GPS longitude (°)")]
    public float? Longitude { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Current (A)")]
    public float? Current { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Motor speed (RPM)")]
    public int? RPM { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Speed (mph)")]
    public float? Speed { get; set; }

    [CsvHelper.Configuration.Attributes.Name("Lap")]
    public int? FileLap { get; set; }
    [CsvHelper.Configuration.Attributes.Name("Total voltage (V)")]
    public float? Voltage { get; set; }
}

public class Lap
{
    //TODO calculate lap statistics dynamicaly for better code but a variable isnt that bad forever like just leave it why bother cause i know later you is fucking lazy
    public List<FileData> Data { get; set; } = new List<FileData>();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan EstimatedTime { get; set; }
    public string LapTime { get; set; } = "";
    public string Driver { get; set; } = "";
    public string Car { get; set; } = "";
    public float MaxSpeed { get; set; }
    public int MaxRPM { get; set; }
    public float MaxCurrent { get; set; }
    public float StartVolt { get; set; }
    public float VoltDrop { get; set; }
    public float AverageCurrent { get; set; }
    public int ID { get; set; }
}

public class Location
{
    public string name { get; set; }
    public float lat { get; set; }
    public float lon { get; set; }
    public int zoom { get; set; }
    public bool lapInFile { get; set; }
}

public class SavedResult
{
    public string url { get; set; }
    public List<ResultEntry> results { get; set; }
}


