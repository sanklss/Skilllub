using System;
using System.Collections.Generic;

namespace SkilllubLearnbox.DTOs;

public class TeacherDashboardDto
{
    public int TotalStudents { get; set; }
    public int ActiveCourses { get; set; }
    public int TotalLessonsCompleted { get; set; }
    public List<TeacherCourseDto> Courses { get; set; } = new();
}