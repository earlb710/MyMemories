using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace MyMemories.Services;

/// <summary>
/// Service for extracting detailed metadata from image files.
/// Extracts dimensions, EXIF data, camera information, GPS location, and more.
/// </summary>
public static class ImageMetadataService
{
    /// <summary>
    /// Extracts comprehensive metadata from an image file.
    /// </summary>
    public static async Task<ImageMetadata?> ExtractMetadataAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(filePath);
            var metadata = new ImageMetadata();

            // Get basic properties
            var basicProps = await file.GetBasicPropertiesAsync();
            metadata.FileSize = basicProps.Size;
            metadata.DateModified = basicProps.DateModified.DateTime;

            // Get image properties
            var imageProps = await file.Properties.GetImagePropertiesAsync();
            metadata.Width = imageProps.Width;
            metadata.Height = imageProps.Height;
            metadata.Title = imageProps.Title;
            metadata.DateTaken = imageProps.DateTaken.Year > 1900 ? (DateTime?)imageProps.DateTaken.DateTime : null;
            metadata.CameraManufacturer = imageProps.CameraManufacturer;
            metadata.CameraModel = imageProps.CameraModel;
            metadata.Orientation = imageProps.Orientation.ToString();

            // Get EXIF data using BitmapDecoder
            using (var stream = await file.OpenReadAsync())
            {
                var decoder = await BitmapDecoder.CreateAsync(stream);
                
                // Get pixel dimensions
                metadata.PixelWidth = decoder.PixelWidth;
                metadata.PixelHeight = decoder.PixelHeight;
                
                // Get DPI
                metadata.DpiX = decoder.DpiX;
                metadata.DpiY = decoder.DpiY;

                // Get EXIF data
                if (decoder.BitmapProperties != null)
                {
                    await ExtractExifDataAsync(decoder.BitmapProperties, metadata);
                }
            }

            return metadata;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts EXIF data from bitmap properties.
    /// </summary>
    private static async Task ExtractExifDataAsync(BitmapPropertiesView properties, ImageMetadata metadata)
    {
        try
        {
            // Try to get EXIF properties
            var exifQuery = "/app1/ifd/exif";
            var gpsQuery = "/app1/ifd/gps";

            // ISO Speed
            metadata.IsoSpeed = await TryGetPropertyAsync<ushort>(properties, $"{exifQuery}/{{ushort=34855}}");

            // Exposure Time (Shutter Speed)
            var exposureTime = await TryGetPropertyAsync<BitmapTypedValue>(properties, $"{exifQuery}/{{ushort=33434}}");
            if (exposureTime?.Value is BitmapPropertySet propSet)
            {
                metadata.ExposureTime = FormatExposureTime(propSet);
            }

            // F-Number (Aperture)
            var fNumber = await TryGetPropertyAsync<BitmapTypedValue>(properties, $"{exifQuery}/{{ushort=33437}}");
            if (fNumber?.Value is BitmapPropertySet fNumPropSet)
            {
                metadata.FNumber = FormatFNumber(fNumPropSet);
            }

            // Focal Length
            var focalLength = await TryGetPropertyAsync<BitmapTypedValue>(properties, $"{exifQuery}/{{ushort=37386}}");
            if (focalLength?.Value is BitmapPropertySet focalPropSet)
            {
                metadata.FocalLength = FormatFocalLength(focalPropSet);
            }

            // Flash
            var flash = await TryGetPropertyAsync<ushort?>(properties, $"{exifQuery}/{{ushort=37385}}");
            if (flash.HasValue)
            {
                metadata.Flash = (flash.Value & 1) == 1 ? "Yes" : "No";
            }

            // GPS Data
            await ExtractGpsDataAsync(properties, gpsQuery, metadata);

            // Software
            metadata.Software = await TryGetPropertyAsync<string>(properties, "/app1/ifd/{{ushort=305}}");

            // Copyright
            metadata.Copyright = await TryGetPropertyAsync<string>(properties, "/app1/ifd/{{ushort=33432}}");

            // Artist/Author
            metadata.Artist = await TryGetPropertyAsync<string>(properties, "/app1/ifd/{{ushort=315}}");

            // Image Description
            metadata.ImageDescription = await TryGetPropertyAsync<string>(properties, "/app1/ifd/{{ushort=270}}");
        }
        catch
        {
            // Silently continue - EXIF data is optional
        }
    }

    /// <summary>
    /// Extracts GPS location data from EXIF.
    /// </summary>
    private static async Task ExtractGpsDataAsync(BitmapPropertiesView properties, string gpsQuery, ImageMetadata metadata)
    {
        try
        {
            // GPS Latitude
            var latRef = await TryGetPropertyAsync<string>(properties, $"{gpsQuery}/{{ushort=1}}");
            var lat = await TryGetPropertyAsync<BitmapTypedValue>(properties, $"{gpsQuery}/{{ushort=2}}");
            
            // GPS Longitude
            var lonRef = await TryGetPropertyAsync<string>(properties, $"{gpsQuery}/{{ushort=3}}");
            var lon = await TryGetPropertyAsync<BitmapTypedValue>(properties, $"{gpsQuery}/{{ushort=4}}");

            if (lat?.Value is BitmapPropertySet latPropSet && lon?.Value is BitmapPropertySet lonPropSet)
            {
                var latitude = ConvertGpsCoordinate(latPropSet, latRef);
                var longitude = ConvertGpsCoordinate(lonPropSet, lonRef);

                if (latitude.HasValue && longitude.HasValue)
                {
                    metadata.GpsLatitude = latitude.Value;
                    metadata.GpsLongitude = longitude.Value;
                    metadata.GpsLocation = $"{latitude:F6}° {latRef}, {longitude:F6}° {lonRef}";
                }
            }
        }
        catch
        {
            // GPS data is optional
        }
    }

    /// <summary>
    /// Tries to get a property value from bitmap properties.
    /// </summary>
    private static async Task<T?> TryGetPropertyAsync<T>(BitmapPropertiesView properties, string propertyPath)
    {
        try
        {
            var value = await properties.GetPropertiesAsync(new[] { propertyPath });
            if (value.TryGetValue(propertyPath, out var result) && result.Value is T typedValue)
            {
                return typedValue;
            }
        }
        catch
        {
            // Property doesn't exist or can't be read
        }

        return default;
    }

    /// <summary>
    /// Formats exposure time (shutter speed) from EXIF rational value.
    /// </summary>
    private static string? FormatExposureTime(BitmapPropertySet propSet)
    {
        try
        {
            if (propSet.TryGetValue("Numerator", out var num) && 
                propSet.TryGetValue("Denominator", out var den) &&
                num.Value is uint numerator && den.Value is uint denominator)
            {
                if (denominator == 0) return null;
                
                if (numerator == 1)
                    return $"1/{denominator} sec";
                
                var seconds = (double)numerator / denominator;
                return seconds >= 1 ? $"{seconds:F1} sec" : $"1/{(int)(1 / seconds)} sec";
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// Formats f-number (aperture) from EXIF rational value.
    /// </summary>
    private static string? FormatFNumber(BitmapPropertySet propSet)
    {
        try
        {
            if (propSet.TryGetValue("Numerator", out var num) && 
                propSet.TryGetValue("Denominator", out var den) &&
                num.Value is uint numerator && den.Value is uint denominator)
            {
                if (denominator == 0) return null;
                
                var fNumber = (double)numerator / denominator;
                return $"f/{fNumber:F1}";
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// Formats focal length from EXIF rational value.
    /// </summary>
    private static string? FormatFocalLength(BitmapPropertySet propSet)
    {
        try
        {
            if (propSet.TryGetValue("Numerator", out var num) && 
                propSet.TryGetValue("Denominator", out var den) &&
                num.Value is uint numerator && den.Value is uint denominator)
            {
                if (denominator == 0) return null;
                
                var focal = (double)numerator / denominator;
                return $"{focal:F0} mm";
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// Converts GPS coordinate from EXIF format to decimal degrees.
    /// </summary>
    private static double? ConvertGpsCoordinate(BitmapPropertySet propSet, string? direction)
    {
        try
        {
            // GPS coordinates are stored as 3 rational numbers: degrees, minutes, seconds
            if (propSet.TryGetValue("0", out var deg) && deg.Value is BitmapPropertySet degSet &&
                propSet.TryGetValue("1", out var min) && min.Value is BitmapPropertySet minSet &&
                propSet.TryGetValue("2", out var sec) && sec.Value is BitmapPropertySet secSet)
            {
                var degrees = GetRationalValue(degSet);
                var minutes = GetRationalValue(minSet);
                var seconds = GetRationalValue(secSet);

                if (degrees.HasValue && minutes.HasValue && seconds.HasValue)
                {
                    var coordinate = degrees.Value + (minutes.Value / 60.0) + (seconds.Value / 3600.0);
                    
                    // Apply negative sign for South/West
                    if (direction == "S" || direction == "W")
                        coordinate = -coordinate;
                    
                    return coordinate;
                }
            }
        }
        catch { }
        
        return null;
    }

    /// <summary>
    /// Gets rational value (numerator/denominator) from property set.
    /// </summary>
    private static double? GetRationalValue(BitmapPropertySet propSet)
    {
        try
        {
            if (propSet.TryGetValue("Numerator", out var num) && 
                propSet.TryGetValue("Denominator", out var den) &&
                num.Value is uint numerator && den.Value is uint denominator)
            {
                if (denominator == 0) return null;
                return (double)numerator / denominator;
            }
        }
        catch { }
        
        return null;
    }
}

/// <summary>
/// Container for image metadata.
/// </summary>
public class ImageMetadata
{
    // Basic Properties
    public ulong FileSize { get; set; }
    public DateTime? DateModified { get; set; }
    public string? Title { get; set; }
    public DateTime? DateTaken { get; set; }
    
    // Dimensions
    public uint Width { get; set; }
    public uint Height { get; set; }
    public uint PixelWidth { get; set; }
    public uint PixelHeight { get; set; }
    public double DpiX { get; set; }
    public double DpiY { get; set; }
    public string? Orientation { get; set; }
    
    // Camera Information
    public string? CameraManufacturer { get; set; }
    public string? CameraModel { get; set; }
    
    // Camera Settings
    public ushort? IsoSpeed { get; set; }
    public string? ExposureTime { get; set; }
    public string? FNumber { get; set; }
    public string? FocalLength { get; set; }
    public string? Flash { get; set; }
    
    // GPS Location
    public double? GpsLatitude { get; set; }
    public double? GpsLongitude { get; set; }
    public string? GpsLocation { get; set; }
    
    // Author & Copyright
    public string? Artist { get; set; }
    public string? Copyright { get; set; }
    public string? Software { get; set; }
    public string? ImageDescription { get; set; }
    
    /// <summary>
    /// Gets the aspect ratio as a formatted string.
    /// </summary>
    public string AspectRatio
    {
        get
        {
            if (PixelWidth == 0 || PixelHeight == 0)
                return "Unknown";
            
            var gcd = Gcd(PixelWidth, PixelHeight);
            var w = PixelWidth / gcd;
            var h = PixelHeight / gcd;
            
            return $"{w}:{h}";
        }
    }
    
    /// <summary>
    /// Gets the megapixel count.
    /// </summary>
    public string Megapixels
    {
        get
        {
            if (PixelWidth == 0 || PixelHeight == 0)
                return "Unknown";
            
            var mp = (PixelWidth * PixelHeight) / 1_000_000.0;
            return $"{mp:F1} MP";
        }
    }
    
    /// <summary>
    /// Calculates greatest common divisor for aspect ratio.
    /// </summary>
    private static uint Gcd(uint a, uint b)
    {
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }
        return a;
    }
}
