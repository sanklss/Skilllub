using System;
using System.Collections.Generic;

namespace SkilllubLearnbox.DTOs;

public class LessonProgressDto
{
    public string LessonId { get; set; } = "";
    public string LessonTitle { get; set; } = "";
    public int LessonOrder { get; set; }
    public bool IsCompleted { get; set; }
    public bool TheoryCompleted { get; set; }
    public bool QuizCompleted { get; set; }
    public bool CodeCompleted { get; set; }
    public int? BestScore { get; set; }
    public DateTime? LastAttempt { get; set; }
    public bool HasQuiz { get; set; }
    public bool HasCodeExercise { get; set; }
    public List<SubmissionDto> Submissions { get; set; } = new();
}