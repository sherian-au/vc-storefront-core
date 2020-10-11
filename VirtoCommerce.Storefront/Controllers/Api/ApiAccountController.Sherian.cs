using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.Storefront.AutoRestClients.NotificationsModuleApi;
using VirtoCommerce.Storefront.AutoRestClients.NotificationsModuleApi.Models;
using VirtoCommerce.Storefront.Domain;
using VirtoCommerce.Storefront.Domain.Common;
using VirtoCommerce.Storefront.Domain.Security;
using VirtoCommerce.Storefront.Domain.Security.Notifications;
using VirtoCommerce.Storefront.Model.Common.Notifications;
using VirtoCommerce.Storefront.Model.Customer;
using VirtoCommerce.Storefront.Model.Security;
using VirtoCommerce.Storefront.Model.Security.Events;

namespace VirtoCommerce.Storefront.Controllers.Api
{
    public partial class ApiAccountController
    {
        // POST: storefrontapi/account/user

        [HttpPost("wam-supplier-user")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<UserActionIdentityResult>> RegisterWamSupplierUser([FromBody] OrganizationUserRegistration registration)
        {
            registration.Role = "WAM-SUPPLIER";

            TryValidateModel(registration);

            UserActionIdentityResult result;

            if (!ModelState.IsValid)
            {
                result = UserActionIdentityResult
                   .Failed(ModelState.Values.SelectMany(x => x.Errors)
                                     .Select(x => new IdentityError {Description = x.ErrorMessage})
                                     .ToArray());

                return result;
            }

            var foundEmail = await _userManager.FindByEmailAsync(registration.Email);
            if (foundEmail != null)
            {
                result = UserActionIdentityResult
                   .Failed(new IdentityError
                    {
                        Code = "DuplicateEmail",
                        Description = "This email is already taken"
                    });

                return BadRequest(result);
            }

            var organization = new Organization {Id = registration.OrganizationId};

            var authorizationResult = await _authorizationService.AuthorizeAsync(User, organization, CanEditOrganizationResourceAuthorizeRequirement.PolicyName);

            if (!authorizationResult.Succeeded)
            {
                return Unauthorized();
            }

            var contact = registration.ToContact();

            contact.OrganizationId = registration.OrganizationId;

            var user = registration.ToUser();

            user.Contact = contact;

            user.StoreId = WorkContext.CurrentStore.Id;

            user.Roles = new List<Role>
            {
                new Role {Name = "WAM-USER"},
                new Role {Name = "WAM-SUPPLIER"}
            };

            var creatingResult = await _userManager.CreateAsync(user, registration.Password);

            result = UserActionIdentityResult.Instance(creatingResult);

            if (result.Succeeded)
            {
                user = await _signInManager.UserManager.FindByNameAsync(user.UserName);
                await _publisher.Publish(new UserRegisteredEvent(WorkContext, user, registration));

                var token = await _signInManager.UserManager.GenerateEmailConfirmationTokenAsync(user);

                var callbackUrl = Url.Action("ConfirmEmail", "Account", new
                {
                    UserId = user.Id,
                    Token = token
                }, Request.Scheme, WorkContext.CurrentStore.Host);

                var emailConfirmationNotification = new EmailConfirmationNotification(WorkContext.CurrentStore.Id, WorkContext.CurrentLanguage)
                {
                    Url = callbackUrl,
                    Sender = "email@sherian.com.au",
                    Recipient = GetUserEmail(user)
                };

                var sendNotificationResult = await SendNotificationAsync(emailConfirmationNotification);

                if (sendNotificationResult.IsSuccess == false)
                {
                    WorkContext.Form.Errors.Add(SecurityErrorDescriber.ErrorSendNotification(sendNotificationResult.ErrorMessage));
                }

                user = await _userManager.FindByNameAsync(user.UserName);
                await _publisher.Publish(new UserRegisteredEvent(WorkContext, user, registration));
                result.MemberId = user.Id;
            }

            return result;
        }

        private async Task<NotificationSendResult> SendNotificationAsync(NotificationBase notification)
        {
            NotificationSendResult result;

            try
            {
                result = await _platformNotificationApi.SendNotificationByRequestAsync(notification.ToNotificationDto());
            }
            catch (Exception exception)
            {
                result = new NotificationSendResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Error occurred while sending notification: {exception.Message}"
                };
            }

            return result;
        }

        private static string GetUserEmail(User user)
        {
            string email = null;
            if (user != null)
            {
                email = user.Email ?? user.UserName;
                if (user.Contact != null)
                {
                    email = user.Contact?.Email ?? email;
                }
            }

            return email;
        }
    }
}
