﻿/*
 * Measurement API controller.
 *
 * @author Michel Megens
 * @email  michel.megens@sonatolabs.com
 */

using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using SensateService.ApiCore.Attributes;
using SensateService.ApiCore.Controllers;
using SensateService.Enums;
using SensateService.Helpers;
using SensateService.Infrastructure.Storage;
using SensateService.Models;
using SensateService.Models.Json.In;
using SensateService.Models.Json.Out;

namespace SensateService.DataApi.Controllers
{
	[Produces("application/json")]
	[Route("[controller]")]
	public class MeasurementsController : AbstractApiController 
	{
		private readonly IMeasurementCache _store;

		public MeasurementsController(IMeasurementCache cache, IHttpContextAccessor ctx) : base(ctx)
		{
			this._store = cache;
		}

		[HttpPost("create")]
		[ReadWriteApiKey]
		[ProducesResponseType(200)]
		public async Task<IActionResult> Create([FromBody] RawMeasurement raw)
		{
			Status status = new Status();

			if(!(this.HttpContext.Items["ApiKey"] is SensateApiKey key)) {
				return this.Forbid();
			}

			if(key.Type != ApiKeyType.SensorKey) {
				status.ErrorCode = ReplyCode.NotAllowed;
				status.Message = "Invalid sensor API key!";

				return this.BadRequest(status);
			}

			status.ErrorCode = ReplyCode.Ok;
			status.Message = "Measurement queued!";

			await this._store.StoreAsync(raw, RequestMethod.HttpPost).AwaitBackground();
			return this.Ok(status);
		}
	}
}