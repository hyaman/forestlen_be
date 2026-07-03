using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ForestIQ.Domain.Models.Dashboard
{
    public class DcPerformanceResponseModel
    {
        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("LiveStats")]
        public DcPerformanceLiveModel? LiveStats { get; set; }

        [JsonPropertyName("History")]
        public List<DcPerformanceHistoryModel>? History { get; set; }
    }

    public class DcPerformanceLiveModel
    {
        [JsonPropertyName("ServerName")]
        public string? ServerName { get; set; }

        [JsonPropertyName("CpuLoad")]
        public double? CpuLoad { get; set; }

        [JsonPropertyName("MemoryUsage")]
        public double? MemoryUsage { get; set; }

        [JsonPropertyName("DiskCFree")]
        public double? DiskCFree { get; set; }

        [JsonPropertyName("DiskCSizeGB")]
        public double? DiskCSizeGB { get; set; }

        [JsonPropertyName("DiskCFreeGB")]
        public double? DiskCFreeGB { get; set; }

        [JsonPropertyName("LocalDisks")]
        public List<LocalDiskData>? LocalDisks { get; set; }

        [JsonPropertyName("TotalProcesses")]
        public int? TotalProcesses { get; set; }

        [JsonPropertyName("VirtualMemorySizeKB")]
        public long? VirtualMemorySizeKB { get; set; }

        [JsonPropertyName("FreeVirtualMemoryKB")]
        public long? FreeVirtualMemoryKB { get; set; }

        [JsonPropertyName("CpuModel")]
        public string? CpuModel { get; set; }

        [JsonPropertyName("LogicalCores")]
        public int? LogicalCores { get; set; }

        [JsonPropertyName("UptimeDays")]
        public double? UptimeDays { get; set; }

        [JsonPropertyName("LastBoot")]
        public DateTime? LastBoot { get; set; }
        
        [JsonPropertyName("OsVersion")]
        public string? OsVersion { get; set; }

        [JsonPropertyName("Environment")]
        public string? Environment { get; set; }

        [JsonPropertyName("Site")]
        public string? Site { get; set; }

        [JsonPropertyName("TopProcesses")]
        public List<ProcessData>? TopProcesses { get; set; }
    }

    public class LocalDiskData
    {
        [JsonPropertyName("DriveLetter")]
        public string? DriveLetter { get; set; }

        [JsonPropertyName("SizeGB")]
        public double? SizeGB { get; set; }

        [JsonPropertyName("FreeGB")]
        public double? FreeGB { get; set; }
    }

    public class ProcessData
    {
        [JsonPropertyName("ProcessName")]
        public string? ProcessName { get; set; }

        [JsonPropertyName("CpuPercent")]
        public double? CpuPercent { get; set; }

        [JsonPropertyName("MemoryMB")]
        public double? MemoryMB { get; set; }

        [JsonPropertyName("Handles")]
        public int? Handles { get; set; }

        [JsonPropertyName("Threads")]
        public int? Threads { get; set; }
    }

    public class DcPerformanceHistoryModel
    {
        [JsonPropertyName("Timestamps")]
        public string Timestamps { get; set; } = string.Empty;

        [JsonPropertyName("CpuLoad")]
        public double CpuLoad { get; set; }

        [JsonPropertyName("MemoryUsage")]
        public double MemoryUsage { get; set; }

        [JsonPropertyName("NetworkIo")]
        public double NetworkIo { get; set; }
    }
}
