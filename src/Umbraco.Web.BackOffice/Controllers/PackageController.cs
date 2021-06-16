using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Packaging;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Semver;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.BackOffice.Extensions;
using Umbraco.Cms.Web.Common.ActionsResults;
using Umbraco.Cms.Web.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Extensions;
using Constants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Cms.Web.BackOffice.Controllers
{
    /// <summary>
    /// A controller used for managing packages in the back office
    /// </summary>
    [PluginController(Constants.Web.Mvc.BackOfficeApiArea)]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessPackages)]
    public class PackageController : UmbracoAuthorizedJsonController
    {
        private readonly IPackagingService _packagingService;
        private readonly IBackOfficeSecurityAccessor _backofficeSecurityAccessor;

        public PackageController(
            IPackagingService packagingService,
            IBackOfficeSecurityAccessor backofficeSecurityAccessor)
        {
            _packagingService = packagingService ?? throw new ArgumentNullException(nameof(packagingService));
            _backofficeSecurityAccessor = backofficeSecurityAccessor ?? throw new ArgumentNullException(nameof(backofficeSecurityAccessor));
        }

        public IEnumerable<PackageDefinition> GetCreatedPackages()
        {
            return _packagingService.GetAllCreatedPackages();
        }

        public ActionResult<PackageDefinition> GetCreatedPackageById(int id)
        {
            var package = _packagingService.GetCreatedPackageById(id);
            if (package == null)
                return NotFound();

            return package;
        }

        public PackageDefinition GetEmpty()
        {
            return new PackageDefinition();
        }

        /// <summary>
        /// Creates or updates a package
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public ActionResult<PackageDefinition> PostSavePackage(PackageDefinition model)
        {
            if (ModelState.IsValid == false)
                return new ValidationErrorResult(new SimpleValidationModel(ModelState.ToErrorDictionary()));

            //save it
            if (!_packagingService.SaveCreatedPackage(model))
                return ValidationErrorResult.CreateNotificationValidationErrorResult(
                        model.Id == default
                            ? $"A package with the name {model.Name} already exists"
                            : $"The package with id {model.Id} was not found");

            _packagingService.ExportCreatedPackage(model);

            //the packagePath will be on the model
            return model;
        }

        /// <summary>
        /// Deletes a created package
        /// </summary>
        /// <param name="packageId"></param>
        /// <returns></returns>
        [HttpPost]
        [HttpDelete]
        public IActionResult DeleteCreatedPackage(int packageId)
        {
            _packagingService.DeleteCreatedPackage(packageId, _backofficeSecurityAccessor.BackOfficeSecurity.GetUserId().ResultOr(0));

            return Ok();
        }

        [HttpGet]
        public IActionResult DownloadCreatedPackage(int id)
        {
            var package = _packagingService.GetCreatedPackageById(id);
            if (package == null)
                return NotFound();

            if (!System.IO.File.Exists(package.PackagePath))
                return ValidationErrorResult.CreateNotificationValidationErrorResult("No file found for path " + package.PackagePath);

            var fileName = Path.GetFileName(package.PackagePath);

            var encoding = Encoding.UTF8;

            var cd = new System.Net.Mime.ContentDisposition
            {
                FileName =  WebUtility.UrlEncode(fileName),
                Inline = false  // false = prompt the user for downloading;  true = browser to try to show the file inline
            };
            Response.Headers.Add("Content-Disposition", cd.ToString());
            // Set custom header so umbRequestHelper.downloadFile can save the correct filename
            Response.Headers.Add("x-filename", WebUtility.UrlEncode(fileName));
            return new FileStreamResult(System.IO.File.OpenRead(package.PackagePath), new MediaTypeHeaderValue("application/octet-stream")
            {
                Charset = encoding.WebName,
            });

        }

        /// <summary>
        /// Returns all installed packages - only shows their latest versions
        /// </summary>
        /// <returns></returns>
        public IEnumerable<InstalledPackage> GetInstalled()
            => _packagingService.GetAllInstalledPackages().ToList();
    }
}
