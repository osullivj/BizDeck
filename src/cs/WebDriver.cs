using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using System;
using System.Threading;

// TODO: WebDriver API should accept a list of steps
// that invoke logic like this...
// driver.Url = "https://bing.com";
// var element = driver.FindElement(By.Id("sb_form_q"));
// element.SendKeys("WebDriver");
// element.Submit();

public class WebDriver
{
    private EdgeDriver driver = null;
	public WebDriver()
	{
        driver = new EdgeDriver();
	}
    ~WebDriver()
    {
        driver.Quit();
    }
}
