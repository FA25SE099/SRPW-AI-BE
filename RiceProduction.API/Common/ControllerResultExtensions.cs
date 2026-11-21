using Microsoft.AspNetCore.Mvc;
using RiceProduction.Application.Common.Models;
using RiceProduction.Application.Common.Models.Response;

namespace RiceProduction.API.Common;

public static class ControllerResultExtensions
{
    public static ActionResult<Result<T>> ToActionResult<T>(this ControllerBase controller, Result<T> result) =>
        result.Succeeded ? controller.Ok(result) : controller.BadRequest(result);

    public static ActionResult<PagedResult<T>> ToActionResult<T>(this ControllerBase controller, PagedResult<T> result) =>
        result.Succeeded ? controller.Ok(result) : controller.BadRequest(result);

    public static IActionResult ToIActionResult<T>(this ControllerBase controller, Result<T> result) =>
        result.Succeeded ? controller.Ok(result) : controller.BadRequest(result);

    public static IActionResult ToIActionResult<T>(this ControllerBase controller, PagedResult<T> result) =>
        result.Succeeded ? controller.Ok(result) : controller.BadRequest(result);

    public static IActionResult ToFileActionResult(this ControllerBase controller, Result<FileExportResult> result) =>
        result.Succeeded && result.Data != null
            ? controller.File(result.Data.fileContents, result.Data.ContentType, result.Data.FileName)
            : controller.BadRequest(result);
}
