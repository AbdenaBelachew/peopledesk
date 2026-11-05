namespace PeopleDesk.Application.Common;

/// <summary>Simple transactional HTML email templates (outbox consumers send these as-is).</summary>
public static class EmailTemplates
{
    private static string Wrap(string title, string body) => $"""
        <!doctype html><html><body style="font-family:Segoe UI,Arial,sans-serif;background:#f5f6f8;padding:24px;">
          <div style="max-width:560px;margin:auto;background:#ffffff;border-radius:8px;border:1px solid #e5e7eb;">
            <div style="background:#0f172a;color:#fff;padding:16px 24px;border-radius:8px 8px 0 0;font-size:18px;font-weight:600;">PeopleDesk</div>
            <div style="padding:24px;">
              <h2 style="margin:0 0 12px;color:#0f172a;font-size:16px;">{title}</h2>
              <div style="color:#334155;font-size:14px;line-height:1.6;">{body}</div>
            </div>
            <div style="padding:16px 24px;color:#94a3b8;font-size:12px;border-top:1px solid #e5e7eb;">This is an automated message from PeopleDesk.</div>
          </div>
        </body></html>
        """;

    public static (string Subject, string Html) VerifyEmail(string firstName, string link) =>
        ("Verify your PeopleDesk email",
         Wrap("Confirm your email",
              $"<p>Hi {Escape(firstName)},</p><p>Please verify your email address to activate your PeopleDesk account.</p>" +
              $"<p><a href=\"{link}\" style=\"background:#0f172a;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none;\">Verify email</a></p>" +
              $"<p>If the button does not work, open this link: <a href=\"{link}\">{link}</a></p>"));

    public static (string Subject, string Html) PasswordReset(string firstName, string link) =>
        ("Reset your PeopleDesk password",
         Wrap("Password reset requested",
              $"<p>Hi {Escape(firstName)},</p><p>We received a request to reset your password. This link expires in 1 hour.</p>" +
              $"<p><a href=\"{link}\" style=\"background:#0f172a;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none;\">Reset password</a></p>" +
              $"<p>If you did not request this, you can safely ignore this email.</p>"));

    public static (string Subject, string Html) Welcome(string firstName, string organizationName) =>
        ("Welcome to PeopleDesk",
         Wrap("Your workspace is ready",
              $"<p>Hi {Escape(firstName)},</p><p>Your organization <b>{Escape(organizationName)}</b> is now on PeopleDesk. " +
              "Sign in to start onboarding your team.</p>"));

    public static (string Subject, string Html) PayslipReady(string firstName, string period, string link) =>
        ($"Your payslip for {period} is ready",
         Wrap("Payslip published",
              $"<p>Hi {Escape(firstName)},</p><p>Your payslip for <b>{Escape(period)}</b> has been published and is available in your self-service portal.</p>" +
              $"<p><a href=\"{link}\" style=\"background:#0f172a;color:#fff;padding:10px 18px;border-radius:6px;text-decoration:none;\">View payslip</a></p>"));

    private static string Escape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
