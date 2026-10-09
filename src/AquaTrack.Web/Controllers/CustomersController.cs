using System.Linq.Expressions;
using AquaTrack.Application.Common.Interfaces;
using AquaTrack.Domain.Entities;
using AquaTrack.Web.Infrastructure;
using AquaTrack.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AquaTrack.Web.Controllers;

[Authorize(Policy = Policies.ManageFarm)]
public class CustomersController(IApplicationDbContext db) : AppController
{
    private static readonly Dictionary<string, Expression<Func<Customer, object>>> Sortable = new()
    {
        ["name"] = c => c.Name,
        ["due"] = c => c.ReceivableBalance,
    };

    public async Task<IActionResult> Index(ListQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Customers";
        ViewData["Subtitle"] = "Who the farm sells to";
        return View(await BuildListAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, ct));

    private async Task<PartyListViewModel<Customer>> BuildListAsync(ListQuery query, CancellationToken ct)
    {
        var customers = db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            customers = customers.Where(c =>
                c.Name.Contains(term) ||
                (c.Phone != null && c.Phone.Contains(term)) ||
                (c.Address != null && c.Address.Contains(term)));
        }

        // The dues filter is what makes this list useful for chasing payment.
        if (Request.Query["dues"] == "true")
            customers = customers.Where(c => c.ReceivableBalance > 0);

        var sorted = customers.ApplySort(query.Sort, query.Desc, Sortable, "name");

        return new PartyListViewModel<Customer>
        {
            Page = await PagedList<Customer>.CreateAsync(sorted, query.Page, query.PageSize, ct),
            Query = query,
            OnlyWithDues = Request.Query["dues"] == "true",
        };
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id, CancellationToken ct)
    {
        if (id is null) return PartialView("_Form", new CustomerFormViewModel());

        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return NotFound();

        return PartialView("_Form", new CustomerFormViewModel
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Email = customer.Email,
            Address = customer.Address,
            Notes = customer.Notes,
            IsActive = customer.IsActive,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CustomerFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        Customer customer;
        if (model.Id is { } id)
        {
            customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new InvalidOperationException($"Customer {id} not found.");
        }
        else
        {
            customer = new Customer();
            db.Customers.Add(customer);
        }

        customer.Name = model.Name.Trim();
        customer.Phone = model.Phone;
        customer.Email = model.Email;
        customer.Address = model.Address;
        customer.Notes = model.Notes;
        customer.IsActive = model.IsActive;

        await db.SaveChangesAsync(ct);
        return Saved(model.Id is null ? $"Customer \"{customer.Name}\" added." : "Customer updated.");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete customer",
            Message = $"Delete \"{customer.Name}\"?",
            Detail = customer.ReceivableBalance > 0
                ? $"This customer still owes {customer.ReceivableBalance:N2}."
                : "Past sales stay in reports.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (customer is null) return NotFound();

        if (customer.ReceivableBalance > 0)
            return Failed("Collect the outstanding balance before deleting this customer.");

        db.Customers.Remove(customer);
        await db.SaveChangesAsync(ct);

        return Saved($"Customer \"{customer.Name}\" deleted.");
    }
}
