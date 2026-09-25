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
    public class LectureHallController : Controller
    {
        private readonly string _conn;

        public LectureHallController(IConfiguration config)
        {
            // FIX: Build absolute path — relative path in appsettings won't resolve in Core
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "timetable.accdb");
            _conn = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath};";
        }

        // GET: /LectureHall
        public IActionResult Index()
        {
            ViewBag.Halls = GetAllHalls();
            return View();
        }

        // POST: /LectureHall/Save
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(LectureHallModel model)
        {
            // FIX: validation was missing entirely
            if (string.IsNullOrWhiteSpace(model.HallName))
            {
                TempData["Error"] = "Hall name cannot be empty.";
                return RedirectToAction("Index");
            }
            if (model.Capacity <= 0)
            {
                TempData["Error"] = "Capacity must be greater than 0.";
                return RedirectToAction("Index");
            }

            using (var con = new OleDbConnection(_conn))
            {
                con.Open();

                // FIX: duplicate hall name check (was missing)
                var chk = new OleDbCommand("SELECT COUNT(*) FROM LectureHall WHERE HallName = ?", con);
                chk.Parameters.AddWithValue("?", model.HallName.Trim());
                if ((int)chk.ExecuteScalar() > 0)
                {
                    TempData["Error"] = $"Hall '{model.HallName}' already exists.";
                    return RedirectToAction("Index");
                }

                // FIX: HallID is AutoNumber — do NOT include it in INSERT (was fine but explicit is safer)
                var cmd = new OleDbCommand("INSERT INTO LectureHall (HallName, Capacity) VALUES (?, ?)", con);
                cmd.Parameters.AddWithValue("?", model.HallName.Trim());
                cmd.Parameters.AddWithValue("?", model.Capacity);
                cmd.ExecuteNonQuery();
            }

            TempData["Message"] = "Lecture hall saved successfully!";
            return RedirectToAction("Index");
        }

        // POST: /LectureHall/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(LectureHallModel model)
        {
            if (string.IsNullOrWhiteSpace(model.HallName) || model.Capacity <= 0)
            {
                TempData["Error"] = "Hall name and valid capacity are required.";
                return RedirectToAction("Index");
            }

            using (var con = new OleDbConnection(_conn))
            {
                con.Open();
                var cmd = new OleDbCommand(
                    "UPDATE LectureHall SET HallName = ?, Capacity = ? WHERE HallID = ?", con);
                cmd.Parameters.AddWithValue("?", model.HallName.Trim());
                cmd.Parameters.AddWithValue("?", model.Capacity);
                cmd.Parameters.AddWithValue("?", model.HallID);
                cmd.ExecuteNonQuery();
            }

            TempData["Message"] = "Lecture hall updated.";
            return RedirectToAction("Index");
        }

        // POST: /LectureHall/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int hallID)
        {
            try
            {
                using (var con = new OleDbConnection(_conn))
                {
                    con.Open();
                    var cmd = new OleDbCommand("DELETE FROM LectureHall WHERE HallID = ?", con);
                    cmd.Parameters.AddWithValue("?", hallID);
                    cmd.ExecuteNonQuery();
                }
                TempData["Message"] = "Lecture hall deleted.";
            }
            catch
            {
                TempData["Error"] = "Cannot delete — this hall is used in the timetable.";
            }

            return RedirectToAction("Index");
        }

        private List<LectureHallModel> GetAllHalls()
        {
            var list = new List<LectureHallModel>();
            using (var con = new OleDbConnection(_conn))
            {
                // FIX: SELECT * replaced with named columns to avoid column-order crashes
                var da = new OleDbDataAdapter(
                    "SELECT HallID, HallName, Capacity FROM LectureHall ORDER BY HallName", con);
                var dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow row in dt.Rows)
                    list.Add(new LectureHallModel
                    {
                        HallID   = (int)row["HallID"],
                        HallName = row["HallName"].ToString(),
                        // FIX: safe parse — Capacity is Number but stored as object in DataRow
                        Capacity = int.TryParse(row["Capacity"].ToString(), out int cap) ? cap : 0
                    });
            }
            return list;
        }
    }
}
