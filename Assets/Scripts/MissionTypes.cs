using System;

public enum FlightMode { Assist, Manual }
public enum MissionState { FreeFlight, Active, Completed, Failed }
public enum StoryStage { Route, Objective, ReturnHome }
public enum ViewMode { Operator, Fpv, Chase }

[Serializable]
public struct MissionResult
{
    public float elapsedSeconds;
    public int collisions;
    public float batteryPercent;
    public int jobPenalty;
    public float landingDistance;
    public int score;
    public string grade;
    public string pilotStyle;
}
