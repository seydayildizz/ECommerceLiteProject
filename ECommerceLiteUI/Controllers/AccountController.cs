﻿using ECommerceLiteBLL.Account;
using ECommerceLiteBLL.Repository;
using ECommerceLiteBLL.Settings;
using ECommerceLiteEntity.Enums;
using ECommerceLiteEntity.IdentityModels;
using ECommerceLiteEntity.Models;
using ECommerceLiteEntity.ViewModels;
using ECommerceLiteUI.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Owin.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace ECommerceLiteUI.Controllers
{
    public class AccountController : BaseController
    {
        //Global Alan
        CustomerRepo myCustomerRepo = new CustomerRepo();
        PassiveUserRepo myPassiveUserRepo = new PassiveUserRepo();
        UserManager<ApplicationUser> myUserManager = MembershipTools.NewUserManager();
        UserStore<ApplicationUser> myUserStore = MembershipTools.NewUserStore();

        RoleManager<ApplicationRole> myRoleManager = MembershipTools.NewRoleManager();


        [HttpGet]
        //KAYIT OL
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken] //token alması için
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            try
            {
                if (!ModelState.IsValid) //false olma durumu
                {
                    return View(model);
                }
                
                var checkUserTC = myUserStore.Context.Set<Customer>().FirstOrDefault(x => x.TCNumber == model.TCNumber)?.TCNumber;
                if (checkUserTC != null)
                {
                    ModelState.AddModelError("", "Bu TC numarası ile daha önceden kayıt mevcut!");
                    return View(model);
                }

                var checkUserEmail = myUserStore.Context.Set<ApplicationUser>().FirstOrDefault(x => x.Email == model.EMail)?.Email;
                if (checkUserEmail != null)
                {
                    ModelState.AddModelError("", "Bu EMail adresi ile sistemde kaydınız mevcuttur! Şifrenizi unuttuysanız Şifremi Unuttum seçeneğine tıklayınız..");
                    return View(model);
                }

                //guidleme işlemi, aradaki - işaretlerini kaldırır
                var theActivationCode = Guid.NewGuid().ToString().Replace("-", "");
                var newUser = new ApplicationUser()
                {
                    Name = model.Name,
                    Surname = model.Surname,
                    Email = model.EMail,
                    ActivationCode = theActivationCode
                };

                var theResult = myUserManager.CreateAsync(newUser, model.Password);
                if (theResult.Result.Succeeded)
                {
                    //Asp.NET Users tablosuna kayıt gerçekleşirse yeni kayıt olmuş bu kişi pasif tablosuna eklenir.
                    //Kişi kendisine gelen aktifleşme işlemini yaparsa  pasif kullanıcılar tablosundan kendini
                    //silip olması gereken roldeki tabloya eklenir.
                    await myUserManager.AddToRoleAsync(newUser.Id, TheIdentityRoles.Passive.ToString());
                    PassiveUser newPassiveUser = new PassiveUser()
                    {
                        TCNumber = model.TCNumber,
                        UserId = newUser.Id,
                        TargetRole = TheIdentityRoles.Customer
                    };
                    myPassiveUserRepo.Insert(newPassiveUser);


                    //kullanıcıya eMail göndermek
                    string siteUrl = Request.Url.Scheme
                        + Uri.SchemeDelimiter
                        + Request.Url.Host
                        + (Request.Url.IsDefaultPort? "" : ":" +Request.Url.Port);

                    await SiteSettings.SendMail(new MailModel()
                    {
                        To = newUser.Email,
                        Subject = "ECommerceLite Site Aktivasyon",
                        Message = $"Merhaba {newUser.Name} {newUser.Surname}, " +
                        $"<br/>Hesabınızı aktifleştirmek için <b>" +
                        $"<a href='{siteUrl}/Account/Activation?code={theActivationCode}'>" +
                        $"Aktivasyon Linkine</a></b> tıklayınız..."
                    });

                    return RedirectToAction("Login","Account",new { email=$"{newUser.Email}"});



                }
                else
                {
                    ModelState.AddModelError("", "Kullanıcı kayıt işlemi sırasında hata oluştu");
                    return View(model);

                }

            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Beklenmedik bir hata oluştu!");
                return View(model);
            }
        }
        [HttpGet]
        public async Task<ActionResult> Activation(string code)
        {
            try
            {
               
                var theUser = myUserStore.Context.Set<ApplicationUser>().FirstOrDefault(x => x.ActivationCode == code);
                if (theUser == null)
                {
                    ViewBag.ActivationResult = "Aktivasyon işlemi  başarısız";
                    return View();
                }

                if (theUser.EmailConfirmed)
                {
                    ViewBag.ActivationResult = "E-Posta adresiniz zaten onaylı";
                    return View();
                }
                theUser.EmailConfirmed = true;
                await myUserStore.UpdateAsync(theUser);
                await myUserStore.Context.SaveChangesAsync();
                //kullanıcıya passiveuser tablosundan bulalım

                PassiveUser thePassiveUser = myPassiveUserRepo.Queryable().FirstOrDefault(x => x.UserId == theUser.Id);

                if (thePassiveUser!=null)
                {
                    if (thePassiveUser.TargetRole == TheIdentityRoles.Customer)
                    {
                        //yeni bir customer oluşacak ve kaydedilecek 
                        Customer newCustomer = new Customer()
                        {
                            TCNumber = thePassiveUser.TCNumber,
                            UserId=thePassiveUser.UserId,
                            
                        };
                        myCustomerRepo.Insert(newCustomer);
                        //pasif tablosundan bu rol silinsin
                        myPassiveUserRepo.Delete(thePassiveUser);
                        //user daki pasif rol silinip customer rol olarak eklenecek 
                        myUserManager.RemoveFromRole(theUser.Id, TheIdentityRoles.Passive.ToString());
                        myUserManager.AddToRole(theUser.Id, TheIdentityRoles.Customer.ToString());
                        ViewBag.ActivationResult = $"Merhaba {theUser.Name} {theUser.Surname} aktivasyon işleminiz başarılıdır..";
                        return View();

                    }
                }
                return View();
            }
            catch (Exception ex)
            {

                //TODO: ex Loglama
                ModelState.AddModelError("", "Beklenmedik bir hata oluştu!");
                return View();
            }


        }

        [HttpGet]
        public ActionResult Login(string ReturnUrl, string email)
        {
            try
            {
                if (HttpContext.User.Identity.IsAuthenticated)
                {
                    var url = ReturnUrl.Split('/');
                    //TODO: Buraya devam edilecek
                }
                var model = new LoginViewModel()
                {
                    ReturnUrl = ReturnUrl
                };
                return View(model);
            }
            catch (Exception ex)
            {
                //ex loglanacak
                return View();
            }

        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }
                var theUser = await myUserManager.FindAsync(model.EMail, model.Password);
                if (theUser == null)
                {
                    ModelState.AddModelError(string.Empty, "EMail veya şifreyi doğru girdiğinizden emin olunuz...");
                    return View(model);
                }
                if (theUser.Roles.FirstOrDefault().RoleId == myRoleManager.FindByName(Enum.GetName(typeof(TheIdentityRoles),TheIdentityRoles.Passive)).Id)
                {
                    ViewBag.theResult = "Sistemi kullanabilmeniz için üyeliğinizi aktifleştirmeniz gerekmektedir. Emailinize gönderilen aktivasyon linkine tıklayarak aktifleştirme işlemini yapabilirsiniz. ";
                    return View(model);
                }
                var authManager = HttpContext.GetOwinContext().Authentication;
                var userIdentity = await myUserManager.CreateIdentityAsync(theUser, DefaultAuthenticationTypes.ApplicationCookie);
                authManager.SignIn(new AuthenticationProperties
                {
                    IsPersistent = model.RememberMe // giriş bilgileri hatırlansın mı ?
                }, userIdentity);
                if (theUser.Roles.FirstOrDefault().RoleId == myRoleManager.FindByName
                    (Enum.GetName(typeof(TheIdentityRoles), TheIdentityRoles.Admin))
                    .Id)
                {
                    return RedirectToAction("Index", "Admin");

                }
                if (theUser.Roles.FirstOrDefault().RoleId == myRoleManager.FindByName
                    (Enum.GetName(typeof(TheIdentityRoles), TheIdentityRoles.Customer))
                    .Id)
                {
                    return RedirectToAction("Index", "Home");

                }

                if (string.IsNullOrEmpty(model.ReturnUrl))
                    return RedirectToAction("Index", "Home");

                var url = model.ReturnUrl.Split('/');
                if (url.Length == 4)
                    return RedirectToAction(url[2], url[1], new { id = url[3] });
                else
                    return RedirectToAction(url[2], url[1]);

            }
            catch (Exception ex)
            {
                //TODO: ex loglanacak
                ModelState.AddModelError("", "Beklenmedik bir hata oluştu!");
                return View(model);
            }
        }
    }
}