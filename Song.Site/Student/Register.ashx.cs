﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using WeiSha.Common;
using Song.ServiceInterfaces;
using Song.Extend;

namespace Song.Site.Student
{
    /// <summary>
    /// 学员注册
    /// </summary>
    public class Register : BasePage
    {
        protected new HttpResponse Response { get; private set; }
        protected override void InitPageTemplate(HttpContext context)
        {
            this.Response = context.Response;
            if (Request.ServerVariables["REQUEST_METHOD"] == "GET")
            {
                //一些设置项
                WeiSha.Common.CustomConfig config = CustomConfig.Load(this.Organ.Org_Config);
                this.Document.SetValue("IsRegStudent", config["IsRegStudent"].Value.Boolean ?? true);   //是否允许注册
                this.Document.SetValue("IsRegSms", config["IsRegSms"].Value.Boolean ?? true);    //是否要短信验证
                //注册协议
                string agreement = Business.Do<IAccounts>().RegAgreement();
                this.Document.SetValue("Agreement", agreement);
            }

            //此页面的ajax提交，全部采用了POST方式
            if (Request.ServerVariables["REQUEST_METHOD"] == "POST")
            {
                string action = WeiSha.Common.Request.Form["action"].String;
                switch (action)
                {
                    case "getSms":
                        mobivcode_verify();  //验证手机登录时，获取短信时的验证码
                        break;
                    case "mobiregister":
                        mobiregister_verify();   //用手机号注册
                        break;
                }
                Response.End();
            }
        }
        /// <summary>
        /// 获取短信之间的验证
        /// </summary>
        private void mobivcode_verify()
        {
            //取图片验证码
            string vname = WeiSha.Common.Request.Form["vname"].String;
            string imgCode = WeiSha.Common.Request.Cookies[vname].ParaValue;
            //取输入的验证码
            string userCode = WeiSha.Common.Request.Form["vcode"].MD5;
            //输入的手机号
            string phone = WeiSha.Common.Request.Form["phone"].String;
            //验证图片验证码
            if (imgCode != userCode)
            {
                Response.Write("{\"success\":\"-1\",\"state\":\"1\"}");   //图片验证码不正确
                return;
            }
            //验证手机号是否存在
            Song.Entities.Accounts acc = Business.Do<IAccounts>().IsAccountsExist(-1, phone, 1);
            if (acc != null)
            {
                Response.Write("{\"success\":\"-1\",\"state\":\"2\"}");   //手机号已经存在
                return;
            }
            //发送短信验证码
            try
            {
                bool success = Business.Do<ISMS>().SendVcode(phone, "reg_mobi_" + vname);
                //bool success = true;
                if (success) Response.Write("{\"success\":\"1\",\"state\":\"0\"}");  //短信发送成功                
            }
            catch (Exception ex)
            {
                Response.Write("{\"success\":\"-1\",\"state\":\"3\",\"desc\":\"" + ex.Message + "\"}");  //短信发送失败   
            }
        }
        /// <summary>
        /// 手机注册的验证
        /// </summary>
        private void mobiregister_verify()
        {
            string vname = WeiSha.Common.Request.Form["vname"].String;
            string imgCode = WeiSha.Common.Request.Cookies[vname].ParaValue;    //取图片验证码
            string userCode = WeiSha.Common.Request.Form["vcode"].MD5;  //取输入的验证码
            string phone = WeiSha.Common.Request.Form["phone"].String;  //输入的手机号
            string sms = WeiSha.Common.Request.Form["sms"].MD5;  //输入的短信验证码
            string pw = WeiSha.Common.Request.Form["pw"].MD5;    //密码
            string name = WeiSha.Common.Request.Form["name"].String;     //姓名
            string email = WeiSha.Common.Request.Form["email"].String;     //邮箱
            string rec = WeiSha.Common.Request.Form["rec"].String;  //推荐人的电话
            int recid = WeiSha.Common.Request.Form["recid"].Int32 ?? 0;  //推荐人的账户id
            //验证图片验证码
            if (imgCode != userCode)
            {
                Response.Write("{\"success\":\"-1\",\"state\":\"1\"}");   //图片验证码不正确
                return;
            }
            //验证手机号是否存在
            Song.Entities.Accounts acc = Business.Do<IAccounts>().IsAccountsExist(-1, phone, 1);
            if (acc != null)
            {
                Response.Write("{\"success\":\"-1\",\"state\":\"2\"}");   //手机号已经存在
                return;
            }
            //验证短信验证码
            bool isSmsCode = true;      //是否短信验证；
            WeiSha.Common.CustomConfig config = CustomConfig.Load(this.Organ.Org_Config);
            isSmsCode = config["IsRegSms"].Value.Boolean ?? true;
            string smsCode = WeiSha.Common.Request.Cookies["reg_mobi_" + vname].ParaValue;
            if (isSmsCode && sms != smsCode)
            {
                Response.Write("{\"success\":\"-1\",\"state\":\"3\"}");  //短信验证失败             
                return;
            }
            else
            {               
                //创建新账户
                Song.Entities.Accounts tmp = new Entities.Accounts();
                tmp.Ac_AccName = phone;
                tmp.Ac_Pw = pw;
                tmp.Ac_Name = name;
                tmp.Ac_MobiTel1 = phone;
                tmp.Ac_Email = email;
                //获取推荐人
                Song.Entities.Accounts accRec = null;
                if (!string.IsNullOrWhiteSpace(rec)) accRec = Business.Do<IAccounts>().AccountsSingle(rec, true, true);
                if (accRec == null && recid > 0) accRec = Business.Do<IAccounts>().AccountsSingle(recid);
                if (accRec != null && accRec.Ac_ID != tmp.Ac_ID)
                {
                    tmp.Ac_PID = accRec.Ac_ID;  //设置推荐人，即：当前注册账号为推荐人的下线                   
                    Business.Do<IAccounts>().PointAdd4Register(accRec);   //增加推荐人积分
                }
                //如果需要审核通过                
                tmp.Ac_IsPass = !(bool)(config["IsVerifyStudent"].Value.Boolean ?? true);
                tmp.Ac_IsUse = tmp.Ac_IsPass;
                int id = Business.Do<IAccounts>().AccountsAdd(tmp);
               
                //以下为判断是否审核通过
                if (tmp.Ac_IsPass)
                {
                    LoginState.Accounts.Write(tmp);
                    Response.Write("{\"success\":\"1\",\"name\":\"" + tmp.Ac_Name + "\",\"acid\":\"" + tmp.Ac_ID + "\",\"state\":\"1\"}");
                }
                else
                {
                    //注册成功，但待审核
                    Response.Write("{\"success\":\"1\",\"name\":\"" + tmp.Ac_Name + "\",\"acid\":\"" + tmp.Ac_ID + "\",\"state\":\"0\"}");
                }
            }
        }        
        
    }
}