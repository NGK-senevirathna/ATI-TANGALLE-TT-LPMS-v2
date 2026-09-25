using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Text.RegularExpressions;
using TimetableApp.Models;
using System;

namespace TimetableApp.Controllers
{
    public class DepartmentController : Controller
    {
        private readonly string _conn;

        public DepartmentController(IConfiguration config)
        {
            // FIX: Build absolute path — relative path in config won't resolve in Core
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "timetable.accdb");
            _conn = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath};";
        }

        // GET: /Department
        public IActionResult Index()
        {
            return View(GetAllDepartments());
        }

        // POST: /Department/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(DepartmentModel model)
        {
            // FIX: validate TAN/XX pattern (was completely missing)
            if (string.IsNullOrWhiteSpace(model.DeptID) ||
                !Regex.IsMatch(model.DeptID.Trim(), @"^TAN\/[A-Za-z]+$"))
            {
                TempData["Error"] = "DeptID must follow the pattern TAN/IT  e.g. TAN/IT, TAN/SCI, TAN/ENG";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(model.DeptName))
            {
                TempData["Error"] = "Department name cannot be empty.";
                return RedirectToAction("Index");
            }

            using (var con = new OleDbConnection(_conn))
            {
                con.Open();

                // FIX: duplicate check (was missing)
                var chk = new OleDbCommand("SELECT COUNT(*) FROM Department WHERE DeptID = ?", con);
                chk.Parameters.AddWithValue("?", model.DeptID.ToUpper());
                if ((int)chk.ExecuteScalar() > 0)
                {
                    TempData["Error"] = $"DeptID '{model.DeptID.ToUpper()}' already exists.";
                    return RedirectToAction("Index");
                }

                // FIX: original INSERT was missing DeptID column — only saved DeptName
                var cmd = new OleDbCommand("INSERT INTO Department (DeptID, DeptName) VALUES (?, ?)", con);
                cmd.Parameters.AddWithValue("?", model.DeptID.ToUpper());
                cmd.Parameters.AddWithValue("?", model.DeptName);
                cmd.ExecuteNonQuery();
            }

            TempData["Message"] = "Department saved successfully!";
            return RedirectToAction("Index");
        }

        // POST: /Department/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(DepartmentModel model)
        {
            if (string.IsNullOrWhiteSpace(model.DeptName))
            {
                TempData["Error"] = "Department name cannot be empty.";
                return RedirectToAction("Index");
            }

            using (var con = new OleDbConnection(_conn))
            {
                con.Open();
                var cmd = new OleDbCommand("UPDATE Department SET DeptName = ? WHERE DeptID = ?", con);
                cmd.Parameters.AddWithValue("?", model.DeptName);
                cmd.Parameters.AddWithValue("?", model.DeptID);
                cmd.ExecuteNonQuery();
            }

            TempData["Message"] = "Department updated.";
            return RedirectToAction("Index");
        }

        // POST: /Department/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string deptID)
        {
            try
            {
                using (var con = new OleDbConnection(_conn))
                {
                    con.Open();
                    var cmd = new OleDbCommand("DELETE FROM Department WHERE DeptID = ?", con);
                    cmd.Parameters.AddWithValue("?", deptID);
                    cmd.ExecuteNonQuery();
                }
                TempData["Message"] = "Department deleted.";
            }
            catch
            {
                TempData["Error"] = "Cannot delete — this department is linked to lecturers or subjects.";
            }

            return RedirectToAction("Index");
        }

        private List<DepartmentModel> GetAllDepartments()
        {
            var list = new List<DepartmentModel>();
            using (var con = new OleDbConnection(_conn))
            {
                var da = new OleDbDataAdapter("SELECT DeptID, DeptName FROM Department ORDER BY DeptID", con);
                var dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow row in dt.Rows)
                    list.Add(new DepartmentModel
                    {
                        // FIX: DeptID is Short Text string, NOT int — (int) cast would crash
                        DeptID   = row["DeptID"].ToString(),
                        DeptName = row["DeptName"].ToString()
                    });
            }
            return list;
        }
    }
}
