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
public class SuppliersController(IApplicationDbContext db) : AppController
{
    private static readonly Dictionary<string, Expression<Func<Supplier, object>>> Sortable = new()
    {
        ["name"] = s => s.Name,
        ["payable"] = s => s.PayableBalance,
    };

    public async Task<IActionResult> Index(ListQuery query, CancellationToken ct)
    {
        ViewData["Title"] = "Suppliers";
        ViewData["Subtitle"] = "Who the farm buys from";
        return View(await BuildListAsync(query, ct));
    }

    [HttpGet]
    public async Task<IActionResult> List(ListQuery query, CancellationToken ct)
        => PartialView("_Table", await BuildListAsync(query, ct));

    private async Task<PartyListViewModel<Supplier>> BuildListAsync(ListQuery query, CancellationToken ct)
    {
        var suppliers = db.Suppliers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            suppliers = suppliers.Where(s =>
                s.Name.Contains(term) ||
                (s.Phone != null && s.Phone.Contains(term)) ||
                (s.ContactPerson != null && s.ContactPerson.Contains(term)));
        }

        var sorted = suppliers.ApplySort(query.Sort, query.Desc, Sortable, "name");

        return new PartyListViewModel<Supplier>
        {
            Page = await PagedList<Supplier>.CreateAsync(sorted, query.Page, query.PageSize, ct),
            Query = query,
        };
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id, CancellationToken ct)
    {
        if (id is null) return PartialView("_Form", new SupplierFormViewModel());

        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null) return NotFound();

        return PartialView("_Form", new SupplierFormViewModel
        {
            Id = supplier.Id,
            Name = supplier.Name,
            ContactPerson = supplier.ContactPerson,
            Phone = supplier.Phone,
            Email = supplier.Email,
            Address = supplier.Address,
            Notes = supplier.Notes,
            IsActive = supplier.IsActive,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SupplierFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Form", model);
        }

        Supplier supplier;
        if (model.Id is { } id)
        {
            supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct)
                       ?? throw new InvalidOperationException($"Supplier {id} not found.");
        }
        else
        {
            supplier = new Supplier();
            db.Suppliers.Add(supplier);
        }

        supplier.Name = model.Name.Trim();
        supplier.ContactPerson = model.ContactPerson;
        supplier.Phone = model.Phone;
        supplier.Email = model.Email;
        supplier.Address = model.Address;
        supplier.Notes = model.Notes;
        supplier.IsActive = model.IsActive;

        await db.SaveChangesAsync(ct);
        return Saved(model.Id is null ? $"Supplier \"{supplier.Name}\" added." : "Supplier updated.");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmDelete(int id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null) return NotFound();

        return PartialView("_Confirm", new ConfirmViewModel
        {
            Title = "Delete supplier",
            Message = $"Delete \"{supplier.Name}\"?",
            Detail = supplier.PayableBalance > 0
                ? $"There is still {supplier.PayableBalance:N2} outstanding to this supplier."
                : "Past purchases stay in reports.",
            Action = Url.Action(nameof(Delete), new { id })!,
        });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (supplier is null) return NotFound();

        if (supplier.PayableBalance > 0)
            return Failed("Settle the outstanding balance before deleting this supplier.");

        db.Suppliers.Remove(supplier);
        await db.SaveChangesAsync(ct);

        return Saved($"Supplier \"{supplier.Name}\" deleted.");
    }
}
