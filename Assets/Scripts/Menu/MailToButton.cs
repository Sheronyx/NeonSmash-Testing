using UnityEngine;
using System;

public class MailToButton : MonoBehaviour
{
    [SerializeField] private string emailAddress = "info@sheronyx.com";
    [SerializeField] private string subject = "Anfrage";
    [SerializeField, TextArea] private string body = "";

    public void OpenMailClient()
    {
        string mailto = $"mailto:{emailAddress}?subject={Escape(subject)}&body={Escape(body)}";
        Application.OpenURL(mailto);
    }

    private static string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return Uri.EscapeDataString(s).Replace("%0D%0A", "%0A");
    }
}
