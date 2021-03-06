﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HGGM.Models.Audit;
using HGGM.Models.Identity;
using HGGM.Services;
using HGGM.Services.Authorization;
using HGGM.Services.Authorization.Simple;
using HGGM.ViewModels;
using LiteDB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace HGGM.Controllers
{
    [Permission(SimplePermissionType.GetUsers)]
    public class UsersController : Controller
    {
        private readonly List<string> _countryList = CultureService.GetCountries();
        private readonly LiteRepository _db;
        private readonly RoleManager<Role> _roleManager;
        private readonly UserManager<User> _userManager;
        private readonly AuditService _audit;
        private readonly SignInManager<User> _signInManager;

        public UsersController(UserManager<User> userManager, RoleManager<Role> roleManager, LiteRepository db, AuditService audit, SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
            _audit = audit;
            _signInManager = signInManager;
        }

        [Permission(SimplePermissionType.EditUsers)]
        public async Task<ActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            return View(user);
        }

        [Permission(SimplePermissionType.EditUsers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Delete(string id, IFormCollection collection)
        {       
            var user = await _userManager.FindByIdAsync(id);
            var before = JsonConvert.SerializeObject(user);
            await _userManager.DeleteAsync(user);
            _audit.Add(new AdminEditUserAudit()
            {
                Before = before,
                After = "User Deleted",
                EditedUser = user.UserName,
                EditedUserId = user.Id,
                Type = "deleted",
                User = _userManager.GetUserName(User),
                UserId = _userManager.GetUserId(User)
            });

            return RedirectToAction(nameof(Index));
        }

        public async Task<ActionResult> Details(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            var roles = _roleManager.Roles
                .Select(r => r.Name)
                .ToDictionary(r => r, r => user.Roles.Contains(r));
            return View(new EditUserViewModel
            {
                User = user,
                Roles = roles,
                Email = user.Email.Address
            });
        }

        [Permission(SimplePermissionType.EditUsers)]
        public async Task<ActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            var roles = _roleManager.Roles
                .Select(r => r.Name)
                .ToDictionary(r => r, r => user.Roles.Contains(r));
            return View(new EditUserViewModel
            {
                Roles = roles,
                User = user,
                CountryList = _countryList,
                Email = user.Email.Address
            });
        }

        private User EditUser(User db, User edited)
        {
            db.UserName = edited.UserName;
            db.Name = edited.Name;
            db.Biography = edited.Biography;
            db.Country = edited.Country;
            db.DateOfBirth = edited.DateOfBirth;
            db.JoinDate = edited.JoinDate;
            db.TeamspeakUID = edited.TeamspeakUID;
            db.Headline = edited.Headline;
            return db;
        }

        [Permission(SimplePermissionType.EditUsers)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(string id, EditUserViewModel uvm)
        {
            if (!ModelState.IsValid) return View(uvm);

            var user = EditUser(await _userManager.FindByIdAsync(id), uvm.User);
            var userOld = _userManager.FindByIdAsync(id);
            var before = JsonConvert.SerializeObject(user, Formatting.Indented);
            user.Email = uvm.Email;
            user.Roles = uvm.Roles.Where(r => r.Value).Select(h => h.Key).ToList();

            var result = await _userManager.UpdateAsync(user);

            _audit.Add(new AdminEditUserAudit()
            {
                Before = before,
                EditedUser = userOld.Result.UserName,
                EditedUserId = user.Id,
                Type = "edited",
                After = JsonConvert.SerializeObject(user, Formatting.Indented),
                User = _userManager.GetUserName(User),
                UserId = _userManager.GetUserId(User)
            });
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                if (uvm.RemoveAvatar)
                {
                    _db.FileStorage.Delete(user.Id);
                }
                return RedirectToAction(nameof(Index));
            }
            foreach (var error in result.Errors) ModelState.AddModelError(error.Code, error.Description);

            return View(uvm);
        }

        public ActionResult Index()
        {
            var users = _db.Fetch<User>(collectionName: "users").ToList();
            return View(users);
        }
    }
}