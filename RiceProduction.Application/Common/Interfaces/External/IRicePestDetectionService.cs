using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RiceProduction.Application.Common.Interfaces.External
{
    // Root response from AI API
    public class AiApiRootResponse
    {
        [JsonPropertyName("results")]
        public List<AiDetectionResponse> Results { get; set; } = new();
    }

    // Individual result for each image
    public class AiDetectionResponse
    {
        [JsonPropertyName("filename")]
        public string Filename { get; set; } = string.Empty;

        [JsonPropertyName("image_width")]
        public int ImageWidth { get; set; }

        [JsonPropertyName("image_height")]
        public int ImageHeight { get; set; }

        [JsonPropertyName("detections")]
        public List<Detection> Detections { get; set; } = new();
    }

    public class Detection
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("class_id")]
        public int ClassId { get; set; }

        [JsonPropertyName("class_name")]
        public string ClassName { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("box")]
        public BoundingBox Box { get; set; } = new();

        [JsonPropertyName("mask")]
        public List<MaskPolygon>? Mask { get; set; }
    }

    public class MaskPolygon
    {
        [JsonPropertyName("points")]
        public List<List<double>> Points { get; set; } = new();
    }

    public class BoundingBox
    {
        [JsonPropertyName("x1")]
        public double X1 { get; set; }

        [JsonPropertyName("y1")]
        public double Y1 { get; set; }

        [JsonPropertyName("x2")]
        public double X2 { get; set; }

        [JsonPropertyName("y2")]
        public double Y2 { get; set; }
    }

    public class PestDetectionResult
    {
        public bool HasPest { get; set; }
        public int TotalDetections { get; set; }
        public List<PestInfo> DetectedPests { get; set; } = new();
        public ImageInfo ImageInfo { get; set; } = new();
    }

    public class PestInfo
    {
        public int Id { get; set; }
        public int ClassId { get; set; }
        public string PestName { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string ConfidenceLevel { get; set; } = string.Empty;
        public BoundingBox Location { get; set; } = new();
        public List<MaskPolygon>? Mask { get; set; }
    }

    public class ImageInfo
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public interface IRicePestDetectionService
    {
        Task<PestDetectionResult> DetectPestAsync(IFormFile file);
    }
}
