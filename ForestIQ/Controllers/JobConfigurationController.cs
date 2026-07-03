using ForestIQ.Domain.DTO;
using ForestIQ.Domain.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ForestIQ.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JobConfigurationController : ControllerBase
    {
        private readonly IJobConfigurationService _jobConfigurationService;

        public JobConfigurationController(IJobConfigurationService jobConfigurationService)
        {
            _jobConfigurationService = jobConfigurationService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<JobConfiguration>>>> GetAllJobs()
        {
            var jobs = await _jobConfigurationService.GetAllJobsAsync();
            return Ok(ApiResponse<IEnumerable<JobConfiguration>>.Ok(jobs));
        }

        [HttpGet("available")]
        public ActionResult<ApiResponse<IEnumerable<AvailableJobDto>>> GetAvailableJobs()
        {
            var availableJobs = _jobConfigurationService.GetAvailableJobNames();
            return Ok(ApiResponse<IEnumerable<AvailableJobDto>>.Ok(availableJobs));
        }

        [HttpPost]
        [Route("UpdateJob")]
        public async Task<ActionResult<ApiResponse<bool>>> UpdateJob([FromBody] UpdateJobRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CronExpression))
            {
                return BadRequest(ApiResponse<bool>.Fail("CronExpression is required.", 400));
            }

            try
            {
                await _jobConfigurationService.UpdateJobAsync(request.jobName, request.CronExpression, request.IsEnabled, request.RetentionDays);
                return Ok(ApiResponse<bool>.Ok(true, "Job updated successfully."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message, 400));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<bool>.Fail(ex.Message, 404));
            }
            catch (Exception)
            {
                return StatusCode(500, ApiResponse<bool>.Fail("An error occurred while updating the job configuration.", 500));
            }
        }
    }
}
