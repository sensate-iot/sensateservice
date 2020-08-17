/*
 * Database seed for the default user roles.
 * 
 * @author Michel Megens
 * @email  michel.megens@sonatolabs.com
 */

using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

using Microsoft.AspNetCore.Identity;

using SensateService.Common.IdentityData.Models;
using SensateService.Infrastructure.Sql;
using SensateService.Constants;
using System;
using SensateService.Helpers;

namespace SensateService.ApiCore.Init
{
	public static class UserRoleSeed
	{
		public static async Task Initialize(SensateSqlContext ctx,
			RoleManager<SensateRole> roles, UserManager<SensateUser> manager)
		{
			if(ctx == null) {
				throw new ArgumentNullException(nameof(ctx));
			}

			if(manager == null) {
				throw new ArgumentNullException(nameof(manager));
			}

			if(roles == null) {
				throw new ArgumentNullException(nameof(roles));
			}

			SensateUser user;
			ctx.Database.EnsureCreated();
			IEnumerable<string> adminroles = new List<string> {
				"Administrators", "Users"
			};

			if(ctx.Roles.Any() || ctx.Users.Any() || ctx.UserRoles.Any())
				return;

			var uroles = new[] {
				new SensateRole {
					Name = UserRoles.Administrator,
					Description = "System administrators",
				},

				new SensateRole {
					Name = UserRoles.NormalUser,
					Description = "Normal users"
				},

				new SensateRole {
					Name = UserRoles.Banned,
					Description = "Banned users"
				}
			};

			foreach(var role in uroles) {
				await roles.CreateAsync(role).AwaitBackground();
			}

			user = new SensateUser {
				Email = "root@example.com",
				FirstName = "System",
				LastName = "Administrator",
				PhoneNumber = "0600000000",
				PhoneNumberConfirmed = true,
				EmailConfirmed = true
			};

			user.UserName = user.Email;
			user.EmailConfirmed = true;
			await manager.CreateAsync(user, "Root1234#xD").AwaitBackground();
			ctx.SaveChanges();
			user = await manager.FindByEmailAsync("root@example.com").AwaitBackground();
			await manager.AddToRolesAsync(user, adminroles).AwaitBackground();
		}
	}
}
