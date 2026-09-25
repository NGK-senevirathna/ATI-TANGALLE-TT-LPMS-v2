using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using TimetableApp.Models;
using System;

namespace TimetableApp.Controllers
{
    public class PreferenceController : Controller
    {
        private readonly string _conn;

        public PreferenceController(IConfiguration config)
        {
            // FIX: absolute path to .accdb
            var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "timetable.accdb");
            _conn = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={dbPath};";
        }

        // GET: /Preference
        public IActionResult Index()
        {
            ViewBag.Lecturers = GetLecturerList();
            ViewBag.Subjects = GetSubjectList();
            ViewBag.Preferences = GetAllPreferences();
            return View();
        }

        // POST: /Preference/Save
        // FIX: Original code used SubjectID (int) and a separate SeqOrder row per subject.
        //      The DB schema has ONE row per lecturer with columns 1stPref..5thPref (SubjectCode strings).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Save(string LecturerID, string Pref1, string Pref2,
                                  string Pref3, string Pref4, string Pref5)
        {
            if (string.IsNullOrWhiteSpace(LecturerID) || LecturerID == "0")
            {
                TempData["Error"] = "Please select a lecturer.";
                return RedirectToAction("Index");
            }
            if (string.IsNullOrWhiteSpace(Pref1))
            {
                TempData["Error"] = "At least the 1st preference is required.";
                return RedirectToAction("Index");
            }

            // A lecturer cannot pick the same subject for more than one choice
            // (e.g. Web Development as both 1st AND 2nd choice).
            var chosen = new[] { Pref1, Pref2, Pref3, Pref4, Pref5 }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (chosen.Distinct().Count() != chosen.Count)
            {
                TempData["Error"] = "The same subject cannot be picked for more than one choice.";
                return RedirectToAction("Index");
            }

            using (var con = new OleDbConnection(_conn))
            {
                con.Open();

                // Check if this lecturer already has a preference row
                var chk = new OleDbCommand(
                    "SELECT COUNT(*) FROM LecturerPreference WHERE LecturerID = ?", con);
                chk.Parameters.AddWithValue("?", LecturerID);
                int exists = (int)chk.ExecuteScalar();

                OleDbCommand cmd;
                if (exists > 0)
                {
                    // UPDATE existing row
                    cmd = new OleDbCommand(
                        @"UPDATE LecturerPreference 
                          SET [1stPref]=?, [2ndPref]=?, [3rdPref]=?, [4thPref]=?, [5thPref]=?
                          WHERE LecturerID = ?", con);
                }
                else
                {
                    // INSERT new row
                    cmd = new OleDbCommand(
                        @"INSERT INTO LecturerPreference
                          (LecturerID, [1stPref], [2ndPref], [3rdPref], [4thPref], [5thPref])
                          VALUES (?, ?, ?, ?, ?, ?)", con);
                }

                // FIX: parameters must be in correct order for Access ? placeholders
                if (exists > 0)
                {
                    // UPDATE: prefs first, then WHERE LecturerID
                    cmd.Parameters.AddWithValue("?", Pref1);
                    cmd.Parameters.AddWithValue("?", NullIfEmpty(Pref2));
                    cmd.Parameters.AddWithValue("?", NullIfEmpty(Pref3));
                    cmd.Parameters.AddWithValue("?", NullIfEmpty(Pref4));
                    cmd.Parameters.AddWithValue("?", NullIfEmpty(Pref5));
                    cmd.Parameters.AddWithValue("?", LecturerID);
                }
                else
                {
                    // INSERT: LecturerID first, then prefs
                    cmd.Parameters.AddWithValue("?", LecturerID);
                    cmd.Parameters.AddWithValue("?", Pref1);
                    cmd.Parameters.AddWithValue("?", NullIfEmpty(Pref2));
                    cmd.Parameters.AddWithValue("?", NullIfEmpty(Pref3));
                    cmd.Parameters.AddWithValue("?", NullIfEmpty(Pref4));
                    cmd.Parameters.AddWithValue("?", NullIfEmpty(Pref5));
                }

                cmd.ExecuteNonQuery();
            }

            TempData["Message"] = "Preferences saved successfully!";
            return RedirectToAction("Index");
        }

        // POST: /Preference/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int prefID)
        {
            using (var con = new OleDbConnection(_conn))
            {
                con.Open();
                var cmd = new OleDbCommand("DELETE FROM LecturerPreference WHERE PrefID = ?", con);
                cmd.Parameters.AddWithValue("?", prefID);
                cmd.ExecuteNonQuery();
            }
            TempData["Message"] = "Preference record deleted.";
            return RedirectToAction("Index");
        }

        // FIX: dropdown uses LecturerID (string), not int "0" sentinel
        private List<DropdownItem> GetLecturerList()
        {
            var items = new List<DropdownItem>
            {
                new DropdownItem { Value = "", Text = "-- Select Lecturer --" }
            };
            using (var con = new OleDbConnection(_conn))
            {
                var da = new OleDbDataAdapter(
                    "SELECT LecturerID, FullName FROM Lecturer ORDER BY FullName", con);
                var dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow row in dt.Rows)
                    items.Add(new DropdownItem
                    {
                        Value = row["LecturerID"].ToString(),
                        Text = row["FullName"].ToString()
                    });
            }
            return items;
        }

        // FIX: dropdown uses SubjectCode (string), NOT SubjectID int
        //      SubjectName shown, SubjectCode saved — matches the DB schema
        private List<DropdownItem> GetSubjectList()
        {
            var items = new List<DropdownItem>
            {
                new DropdownItem { Value = "", Text = "-- None --" }
            };
            using (var con = new OleDbConnection(_conn))
            {
                var da = new OleDbDataAdapter(
                    "SELECT SubjectCode, SubjectName FROM Subject ORDER BY SubjectName", con);
                var dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow row in dt.Rows)
                    items.Add(new DropdownItem
                    {
                        Value = row["SubjectCode"].ToString(),   // saved to DB
                        Text = row["SubjectName"].ToString()    // shown to user
                    });
            }
            return items;
        }

        // FIX: JOIN shows subject names for all 5 pref columns — original used SubjectID joins
        private List<PreferenceModel> GetAllPreferences()
        {
            var list = new List<PreferenceModel>();
            using (var con = new OleDbConnection(_conn))
            {
                string sql = @"SELECT lp.PrefID,
                      l.FullName       AS LecturerName,
                      s1.SubjectName   AS Pref1Name,
                      s2.SubjectName   AS Pref2Name,
                      s3.SubjectName   AS Pref3Name,
                      s4.SubjectName   AS Pref4Name,
                      s5.SubjectName   AS Pref5Name
               FROM ((((((LecturerPreference lp
               INNER JOIN Lecturer l ON lp.LecturerID = l.LecturerID)
               LEFT JOIN Subject s1 ON lp.[1stPref] = s1.SubjectCode)
               LEFT JOIN Subject s2 ON lp.[2ndPref] = s2.SubjectCode)
               LEFT JOIN Subject s3 ON lp.[3rdPref] = s3.SubjectCode)
               LEFT JOIN Subject s4 ON lp.[4thPref] = s4.SubjectCode)
               LEFT JOIN Subject s5 ON lp.[5thPref] = s5.SubjectCode)
               ORDER BY l.FullName";

                var da = new OleDbDataAdapter(sql, con);
                var dt = new DataTable();
                da.Fill(dt);
                foreach (DataRow row in dt.Rows)
                    list.Add(new PreferenceModel
                    {
                        PrefID = (int)row["PrefID"],
                        LecturerName = row["LecturerName"].ToString(),
                        Pref1Name = row["Pref1Name"].ToString(),
                        Pref2Name = row["Pref2Name"].ToString(),
                        Pref3Name = row["Pref3Name"].ToString(),
                        Pref4Name = row["Pref4Name"].ToString(),
                        Pref5Name = row["Pref5Name"].ToString()
                    });
            }
            return list;
        }

        private object NullIfEmpty(string val) =>
            string.IsNullOrWhiteSpace(val) ? (object)System.DBNull.Value : val;
    }
}