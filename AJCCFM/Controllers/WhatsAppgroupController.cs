﻿using AJCCFM.Models.SocialNetWorking;
using AJESActiveDirectoryInterface;
using AJESeForm.Models;
using Core.Domain;
using Model;
using Services.GroupRequest;
using Services.Helper;
using Services.WhatsAppGroup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace AJCCFM.Controllers
{
    public class WhatsAppgroupController : Controller
    {
        private IWhatsAppGroup _WhatsAppGroup;

        private IGroupRequest _GroupRequest;

    
        private string mailcontent;
        private string body;

        public ActionResult Add()
        {
            List<UserDetail> lADUser;

            var post = new WhatsAppGroup();
            string Empcode = AJESAD.GetEmpNo(System.Web.HttpContext.Current.User.Identity.Name.Replace("AJES\\", ""));

            var empDetail = Common.GetEmpData<Core.EmployeeDetail>(Empcode);
            post.Name = empDetail.EmpName;
            post.EmpCode = empDetail.EmpCode;
            post.Position = empDetail.Position;
            post.Project = empDetail.Project;
            post.ProjectCode = empDetail.ProjectCode;


            if (empDetail.ProjectCode == "8000" || empDetail.ProjectCode == "8000_1")
            {
                AJESActiveDirectoryInterface.AJESAD.RoleID = 1036; //Support office HOD 
                lADUser = AJESActiveDirectoryInterface.AJESAD.GetADUsers();
                ViewBag.ForemanCode = empDetail.ForemanCode;
            }
            else
            {
                AJESActiveDirectoryInterface.AJESAD.RoleID = 1037;
                lADUser = AJESActiveDirectoryInterface.AJESAD.GetADUsers();

            }
            UserDetail userdetail = new UserDetail();
            userdetail.DisplayText = "Please Select";
            // userdetail.LoginName = "X";
            lADUser.Add(userdetail);
            ViewBag.ADUser = lADUser;
            return View(post);


        }


        [ValidateInput(false)]
        [HttpPost]
        public async Task<ActionResult> SubmitRequest(WhatsAppGroup model, string forwardto, string forwardName)
        {
            _WhatsAppGroup = new WhatsAppGroupService();
            _GroupRequest = new GroupRequestService();

            model.Email = AJESActiveDirectoryInterface.AJESAD.GetEmpEmail(model.EmpCode);
            model.Createdby = System.Web.HttpContext.Current.User.Identity.Name.Replace("AJES\\", "");
            model.SubmittedToEmail = AJESActiveDirectoryInterface.AJESAD.GetEmailAddress(forwardto);
            model.SubmittedTo = forwardto;

            string[] Name = forwardName.Split('-');

            RefNoID result = await _WhatsAppGroup.SubmitLinkedInRequest(model);

            if (result == null)
            {
                return RedirectToAction("Add");
            }

            string mGuid = Guid.NewGuid().ToString();

            await _GroupRequest.LogEmail(result.ID, mGuid, "G");


            string url = System.Configuration.ConfigurationManager.AppSettings.Get("Url");
            string Link = url + "/WhatsAppgroup/ShowRequest?token=" + mGuid;
            EmailManager VCTEmailService = new EmailManager();

            body = VCTEmailService.GetBody(Server.MapPath("~/") + "\\App_Data\\Templates\\WhatsAppRequestDH.html");
            mailcontent = body.Replace("@ReqNo", result.RefNo); //Replace Contenct...
            mailcontent = mailcontent.Replace("@pwdchangelink", Link); //Replace Contenct...
            VCTEmailService.Body = mailcontent;
            VCTEmailService.Subject = System.Configuration.ConfigurationManager.AppSettings.Get("WhatsAppSubject");
            VCTEmailService.ReceiverAddress = model.SubmittedToEmail;
            VCTEmailService.ReceiverDisplayName = "";
            await VCTEmailService.SendEmail();

            //Users 

            if (!string.IsNullOrEmpty(model.Email))
            {
                //Send Email to requestor
                EmailManager VCTEmailServiceInit = new EmailManager();
                body = VCTEmailServiceInit.GetBody(Server.MapPath("~/") + "\\App_Data\\Templates\\WhatsAppRequest-Init.html");
                mailcontent = body.Replace("@ReqNo", result.RefNo); //Replace Contenct...
                mailcontent = mailcontent.Replace("@forwardto", Name[0]); //Replace Contenct...
                VCTEmailServiceInit.Body = mailcontent;
                VCTEmailServiceInit.Subject = System.Configuration.ConfigurationManager.AppSettings.Get("WhatsAppSubject");
                VCTEmailServiceInit.ReceiverAddress = model.Email;
                VCTEmailServiceInit.ReceiverDisplayName = model.Name;
                await VCTEmailServiceInit.SendEmail();
            }
            return RedirectToAction("Index", "Dashboard");

        }


        public async Task<ActionResult> ShowRequest(string token)
        {

            _WhatsAppGroup = new WhatsAppGroupService();
            _GroupRequest = new GroupRequestService();

            int TransactionID = await _GroupRequest.GetToken(token, "G");

            if (TransactionID > 0)
            {
                WhatsAppGroupRequestModel obj = _WhatsAppGroup.ViewRequest<WhatsAppGroupRequestModel>(TransactionID);
           
                return View("ViewRequest", obj);
            }
            else
            {
                ViewBag.Token = token;

                return View("Processed");
            }
        }

        public ActionResult ViewRequest(int TransactionID, string mode)
        {

            ViewBag.Mode = mode;
            _WhatsAppGroup = new WhatsAppGroupService();
            WhatsAppGroupRequestModel obj = _WhatsAppGroup.ViewRequest<WhatsAppGroupRequestModel>(TransactionID);

           

            return View(obj);
        }



        public async Task<ActionResult> ApproveRequest(int ID, int Status, string RefNo, string Email, string Remarks)
        {
            _WhatsAppGroup = new WhatsAppGroupService();
            _GroupRequest = new GroupRequestService();
            string Submitedto = "";
            if (Status == 0) // PM Level
            {
                Status = 1;
                Submitedto = System.Configuration.ConfigurationManager.AppSettings.Get("HRForwardTo");
            }
            else
            {
                Status = -1;

            }

            var affectedRows = await _WhatsAppGroup.SubmitForApproval(ID, Status, Submitedto, Remarks);

            //Send Email to 
            string returnURL = "";
            string body;
            if (Status == 1)
            {
                string mGuid = Guid.NewGuid().ToString();
                string url = System.Configuration.ConfigurationManager.AppSettings.Get("Url");
                string Link = url + "/WhatsAppgroup/ShowRequest?token=" + mGuid + "&Mode=E";
                await _GroupRequest.LogEmail(ID, mGuid, "G");
                EmailManager VCTEmailService = new EmailManager();
                body = VCTEmailService.GetBody(Server.MapPath("~/") + "\\App_Data\\Templates\\WhatsAppRequest-HRManager.html");
                mailcontent = body.Replace("@pwdchangelink", Link); //Replace Contenct...
                mailcontent = mailcontent.Replace("@ReqNo", RefNo); //Replace Contenct...
                VCTEmailService.Body = mailcontent;
                VCTEmailService.Subject = System.Configuration.ConfigurationManager.AppSettings.Get("WhatsAppSubject");
                VCTEmailService.ReceiverAddress = System.Configuration.ConfigurationManager.AppSettings.Get("HRManagerEmail");
                await VCTEmailService.SendEmail();


                if (!string.IsNullOrEmpty(Email))
                {

                    //Send Email to User .... 
                    EmailManager VCTEmailServiceUser = new EmailManager();
                    body = VCTEmailServiceUser.GetBody(Server.MapPath("~/") + "\\App_Data\\Templates\\WhatsAppStatusUpdate-Approved.html");
                    mailcontent = body.Replace("@ReqNo", RefNo); //Replace Contenct...
                    VCTEmailServiceUser.Body = mailcontent;
                    VCTEmailServiceUser.Subject = System.Configuration.ConfigurationManager.AppSettings.Get("WhatsAppSubject");
                    VCTEmailServiceUser.ReceiverAddress = Email;
                    await VCTEmailServiceUser.SendEmail();
                    returnURL = Url.Action("Index", "Dashboard");
                }
            }
            else if (Status == -1) //Approved //Send Email to User  and IT HELP DESK AS WELL 
            {

                if (!string.IsNullOrEmpty(Email))
                {
                    EmailManager VCTEmailService = new EmailManager();
                    body = VCTEmailService.GetBody(Server.MapPath("~/") + "\\App_Data\\Templates\\WhatsAppStatusUpdate-Processed.html");
                    mailcontent = body.Replace("@ReqNo", RefNo); //Replace Contenct...
                    VCTEmailService.Body = mailcontent;
                    VCTEmailService.Subject = System.Configuration.ConfigurationManager.AppSettings.Get("WhatsAppSubject");
                    VCTEmailService.ReceiverAddress = Email;
                    await VCTEmailService.SendEmail();
                }

                EmailManager VCTEmailServiceIT = new EmailManager();
                body = VCTEmailServiceIT.GetBody(Server.MapPath("~/") + "\\App_Data\\Templates\\WhatsAppRequestStatus-Approved(IT).html");
                mailcontent = body.Replace("@ReqNo", RefNo); //Replace Contenct...
                VCTEmailServiceIT.Body = mailcontent;
                VCTEmailServiceIT.Subject = System.Configuration.ConfigurationManager.AppSettings.Get("WhatsAppSubject");
                VCTEmailServiceIT.ReceiverAddress = System.Configuration.ConfigurationManager.AppSettings.Get("groupdistribution");
                await VCTEmailServiceIT.SendEmail();

                returnURL = Url.Action("Index", "Dashboard");

            }
            return Json(new { Result = returnURL });
        }

    }
}