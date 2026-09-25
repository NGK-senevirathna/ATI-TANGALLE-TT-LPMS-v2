using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using TimetableApp.Models;
using System;

namespace TimetableApp.Controllers
{
    public class SubjectController : Controller
    {
        private readonly string _conn;

        public SubjectController(IConfiguration config)
        {
            // FIX: absolute path to .accdb
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "timetable.accdb");
            _conn = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath};";
        }

        // GET: /Subject
        public IActionResult Index()
        {
            ViewBag.Departments = GetDepartmentList();
            ViewBag.Subjects    = GetAllSubjects();
            return View(new SubjectModel());
        }

        // POST: /Subject/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(SubjectModel model)
        {
            // FIX: validation was missing
            if (string.IsNullOrWhiteSpace(model.SubjectCode))
            {
                TempData["Error"] = "Subject code cannot be empty.";
                return RedirectToAction("Index");
            }
            if (string.IsNullOrWhiteSpace(model.SubjectName))
            {
                TempData["Error"] = "Subject name cannot be empty.";
                return RedirectToAction("Index");
            }
            if (string.IsNullOrWhiteSpace(model.DeptID))
            {
                TempData["Error"] = "Please select a department.";
                return RedirectToAction("Index");
            }

            using (var con = new OleDbConnection(_conn))
            {
                con.Open();

                // FIX: duplicate SubjectCode check
                var chk = new OleDbCommand("SELECT COUNT(*) FROM Subject WHERE SubjectCode = ?", con);
                chk.Parameters.AddWithValue("?", model.SubjectCode.Trim().ToUpper());
                if ((int)chk.ExecuteScalar() > 0)
                {
                    TempData["Error"] = $"Subject code '{model.SubjectCode}' already exists.";
                    return RedirectToAction("Index");
                }

                // FIX: ContactHoursPerWeek column was missing from INSERT
                var cmd = new OleDbCommand(
                    @"INSERT INTO Subject (SubjectCode, SubjectName, DeptID, ContactHoursPerWeek)
                      VALUES (?, ?, ?, ?)", con);
                cmd.Parameters.AddWithValue("?", model.SubjectCode.Trim().ToUpper());
                cmd.Parameters.AddWithValue("?", model.SubjectName.Trim());
                cmd.Parameters.AddWithValue("?", model.DeptID);
                // FIX: store as string — ContactHoursPerWeek is Short Text in the DB schema
                cmd.Parameters.AddWithValue("?", model.ContactHoursPerWeek?.Trim() ?? "");
                cmd.ExecuteNonQuery();
            }

            TempData["Message"] = "Subject saved successfully!";
            return RedirectToAction("Index");
        }

        // POST: /Subject/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(SubjectModel model)
        {
            if (string.IsNullOrWhiteSpace(model.SubjectName) || string.IsNullOrWhiteSpace(model.DeptID))
            {
                TempData["Error"] = "Subject name and department are required.";
                return RedirectToAction("Index");
            }

            using (var con = new OleDbConnection(_conn))
            {
                con.Open();
                var cmd = new OleDbCommand(
                    @"UPDATE Subject SET SubjectName = ?, DeptID = ?, ContactHoursPerWeek = ?
                      WHERE SubjectCode = ?", con);
                cmd.Parameters.AddWithValue("?", model.SubjectName.Trim());
                cmd.Parameters.AddWithValue("?", model.DeptID);
                cmd.Parameters.AddWithValue("?", model.ContactHoursPerWeek?.Trim() ?? "");
                cmd.Parameters.AddWithValue("?", model.SubjectCode);
                cmd.ExecuteNonQuery();
            }

            TempData["Message"] = "Subject updated.";
            return RedirectToAction("Index");
        }

        // POST: /Subject/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string subjectCode)
        {
            try
            {
                using (var con = new OleDbConnection(_conn))
                {
                    con.Open();
                    var cmd = new OleDbCommand("DELETE FROM Subject WHERE SubjectCode = ?", con);
                    cmd.Parameters.AddWithValue("?", subjectCode);
                    cmd.ExecuteNonQuery();
                }
                TempData["Message"] = "Subject deleted.";
            }
            catch
            {
                TempData["Error"] = "Cannot delete — this subject is used in preferences or the timetable.";
            }

            return RedirectToAction("Index");
        }

        private List<SelectListItem> GetDepartmentList()
        {
            var items = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Department --" }
            };
            using (var con = new OleDbConnection(_conn))
            {
                var da = new OleDbDataAdapter(
                    "SELECT DeptID, DeptName FROM Department ORDER BY DeptName", con);
                var dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow row in dt.Rows)
                    items.Add(new SelectListItem
                    {
                        Value = row["DeptID"].ToString(),
                        Text  = row["DeptName"].ToString()
                    });
            }
            return items;
        }

        private List<SubjectModel> GetAllSubjects()
        {
            var list = new List<SubjectModel>();
            using (var con = new OleDbConnection(_conn))
            {
                string sql = @"SELECT s.SubjectCode, s.SubjectName, s.ContactHoursPerWeek, d.DeptName
                               FROM (Subject s
                               INNER JOIN Department d ON s.DeptID = d.DeptID)
                               ORDER BY s.SubjectName";
                var da = new OleDbDataAdapter(sql, con);
                var dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow row in dt.Rows)
                    list.Add(new SubjectModel
                    {
                        // FIX: SubjectCode is Short Text PK — no SubjectID int column in the schema
                        SubjectCode          = row["SubjectCode"].ToString(),
                        SubjectName          = row["SubjectName"].ToString(),
                        ContactHoursPerWeek  = row["ContactHoursPerWeek"].ToString(),
                        DeptName             = row["DeptName"].ToString()
                    });
            }
            return list;
        }
    }
}
