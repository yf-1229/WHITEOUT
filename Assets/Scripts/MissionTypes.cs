using System;

public enum FlightMode { Assist, Manual }
public enum MissionType { Delivery, Rescue, Inspection }
public enum MissionState { Ready, Active, Completed, Failed }
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
}
