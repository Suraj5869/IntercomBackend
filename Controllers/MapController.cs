using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RiderIntercom.Services;

namespace RiderIntercom.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class MapController : ControllerBase
    {
        private readonly MapService _mapService;

        public MapController(MapService mapService)
        {
            _mapService = mapService;
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search(
            [FromQuery] string q,
            [FromQuery] double? lat = null,
            [FromQuery] double? lng = null)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Ok(Array.Empty<object>());
            }

            var query = q.Trim();

            if (query.Length < 3)
            {
                return Ok(Array.Empty<object>());
            }

            if (query.Length > 256)
            {
                return BadRequest(new { message = "Search query is too long." });
            }

            if ((lat.HasValue && (lat < -90 || lat > 90)) ||
                (lng.HasValue && (lng < -180 || lng > 180)))
            {
                return BadRequest(new { message = "Invalid search coordinates." });
            }

            return Ok(await _mapService.SearchAsync(query, lat, lng));
        }

        [HttpGet("route")]
        public async Task<IActionResult> Route(
            [FromQuery] double fromLat,
            [FromQuery] double fromLng,
            [FromQuery] double toLat,
            [FromQuery] double toLng,
            [FromQuery] string travelMode = "car")
        {
            if (!IsValidCoordinate(fromLat, fromLng) ||
                !IsValidCoordinate(toLat, toLng))
            {
                return BadRequest(new { message = "Invalid route coordinates." });
            }

            if (!string.Equals(travelMode, "car", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(travelMode, "bike", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Invalid travel mode. Use 'car' or 'bike'." });
            }

            return Ok(
                await _mapService.GetRouteAsync(
                    fromLat,
                    fromLng,
                    toLat,
                    toLng,
                    travelMode));
        }

        private static bool IsValidCoordinate(double lat, double lng)
        {
            return lat >= -90 &&
                   lat <= 90 &&
                   lng >= -180 &&
                   lng <= 180;
        }
    }
}
