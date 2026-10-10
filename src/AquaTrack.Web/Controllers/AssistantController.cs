using AquaTrack.Application.Ai;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AquaTrack.Web.Controllers;

/// <summary>
/// AI-5: plain-language questions answered from the farm's own data. Each question is matched to
/// an intent that maps to a real query, so an answer is never invented.
/// </summary>
public class AssistantController(ISmartAssistant assistant) : AppController
{
    public IActionResult Index()
    {
        ViewData["Title"] = "Assistant";
        ViewData["Subtitle"] = "Ask about costs, stock, sales or cash";

        return View(new AssistantViewModel { Suggestions = assistant.SampleQuestions });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ask(string question, CancellationToken ct)
    {
        var answer = await assistant.AskAsync(question, ct);

        return PartialView("_Answer", new AssistantViewModel
        {
            Question = question,
            Answer = answer,
            Suggestions = assistant.SampleQuestions,
        });
    }
}
