using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using TimetableApp.Models;
using System;

namespace TimetableApp.Controllers
{
    public class LecturerController : Controller
    {
        private readonly string _conn;

        public LecturerController(IConfiguration config)
        {
            // FIX: absolute path to .accdb
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "timetable.accdb");
            _conn = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath};";
        }

        // GET: /Lecturer
        public IActionResult Index()
        {
            ViewBag.Lecturers   = GetAllLecturers();
            ViewBag.Departments = GetDepartmentList();
            return View();
        }

        // POST: /Lecturer/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(LecturerModel model)
        {
            // FIX: input validation was missing
            if (string.IsNullOrWhiteSpace(model.LecturerID))
            {
                TempData["Error"] = "Lecturer ID cannot be empty.";
                return RedirectToAction("Index");
            }
            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                TempData["Error"] = "Full name cannot be empty.";
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

                // FIX: duplicate LecturerID check
                var chk = new OleDbCommand("SELECT COUNT(*) FROM Lecturer WHERE LecturerID = ?", con);
                chk.Parameters.AddWithValue("?", model.LecturerID.Trim());
                if ((int)chk.ExecuteScalar() > 0)
                {
                    TempData["Error"] = $"Lecturer ID '{model.LecturerID}' already exists.";
                    return RedirectToAction("Index");
                }

                // FIX: original INSERT was missing LecturerID column
                var cmd = new OleDbCommand(
                    "INSERT INTO Lecturer (LecturerID, FullName, Email, DeptID) VALUES (?, ?, ?, ?)", con);
                cmd.Parameters.AddWithValue("?", model.LecturerID.Trim());
                cmd.Parameters.AddWithValue("?", model.FullName.Trim());
                cmd.Parameters.AddWithValue("?", model.Email?.Trim() ?? "");
                cmd.Parameters.AddWithValue("?", model.DeptID);
                cmd.ExecuteNonQuery();
            }

            TempData["Message"] = "Lecturer saved successfully!";
            return RedirectToAction("Index");
        }

        // POST: /Lecturer/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(LecturerModel model)
        {
            if (string.IsNullOrWhiteSpace(model.FullName) || string.IsNullOrWhiteSpace(model.DeptID))
            {
                TempData["Error"] = "Full name and department are required.";
                return RedirectToAction("Index");
            }

            using (var con = new OleDbConnection(_conn))
            {
                con.Open();
                var cmd = new OleDbCommand(
                    "UPDATE Lecturer SET FullName = ?, Email = ?, DeptID = ? WHERE LecturerID = ?", con);
                cmd.Parameters.AddWithValue("?", model.FullName.Trim());
                cmd.Parameters.AddWithValue("?", model.Email?.Trim() ?? "");
                cmd.Parameters.AddWithValue("?", model.DeptID);
                cmd.Parameters.AddWithValue("?", model.LecturerID);
                cmd.ExecuteNonQuery();
            }

            TempData["Message"] = "Lecturer updated.";
            return RedirectToAction("Index");
        }

        // POST: /Lecturer/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(string lecturerID)
        {
            try
            {
                using (var con = new OleDbConnection(_conn))
                {
                    con.Open();
                    var cmd = new OleDbCommand("DELETE FROM Lecturer WHERE LecturerID = ?", con);
                    cmd.Parameters.AddWithValue("?", lecturerID);
                    cmd.ExecuteNonQuery();
                }
                TempData["Message"] = "Lecturer deleted.";
            }
            catch
            {
                TempData["Error"] = "Cannot delete — this lecturer has preferences or timetable entries.";
            }

            return RedirectToAction("Index");
        }

        private List<LecturerModel> GetAllLecturers()
        {
            var list = new List<LecturerModel>();
            using (var con = new OleDbConnection(_conn))
            {
                string sql = @"SELECT l.LecturerID, l.FullName, l.Email, d.DeptName
                               FROM (Lecturer l
                               INNER JOIN Department d ON l.DeptID = d.DeptID)
                               ORDER BY l.FullName";
                var da = new OleDbDataAdapter(sql, con);
                var dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow row in dt.Rows)
                    list.Add(new LecturerModel
                    {
                        // FIX: LecturerID is Short Text, NOT int — (int) cast would crash
                        LecturerID = row["LecturerID"].ToString(),
                        FullName   = row["FullName"].ToString(),
                        Email      = row["Email"].ToString(),
                        DeptName   = row["DeptName"].ToString()
                    });
            }
            return list;
        }

        private List<DropdownItem> GetDepartmentList()
        {
            var items = new List<DropdownItem>
            {
                new DropdownItem { Value = "", Text = "-- Select Department --" }
            };
            using (var con = new OleDbConnection(_conn))
            {
                var da = new OleDbDataAdapter(
                    "SELECT DeptID, DeptName FROM Department ORDER BY DeptName", con);
                var dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow row in dt.Rows)
                    items.Add(new DropdownItem
                    {
                        Value = row["DeptID"].ToString(),
                        Text  = row["DeptName"].ToString()
                    });
            }
            return items;
        }
    }
}
