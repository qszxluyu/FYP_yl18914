using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public class RacketMovement : MonoBehaviour {
    //Local playback record
    public bool RecordTest = true;
    public bool RecordPlaybackRaw = true;
    public bool RecordPlayback = false;
    private StreamWriter SW;
    private FileInfo PlaybackFile;

    //Used for testing
    private float WholeDispX=0, WholeDispY=0, WholeDispZ=0;

    //Parameters used for serial input
    private string WholeLine;
    private string[] QAData;

    [Tooltip("Set to maximum loop count to avoid initial loop goes to infinity")]
    public bool MaxLoopControl = true;
    public int MaxLoopCount = 20000; //Set to maximum loop count to avoid initial loop goes to infinity

    //Parameters used for reset
    private Vector3 OriginalPosition;

    //Parameter used for threadhold and zero velocity
    public float MagnitudeAccelThreshold = 0.25f;

    [Tooltip("The number of successive zero acceleration states to activate zero velocity calibration")]
    public int ZeroVelocityLimit = 5;
    private int ZeroAccelCount;

    //Parameters used for steady state offset
    [Tooltip("The number of sample taken for the steay-state")]
    public int SStateLength = 32;
    private float SStateAccelX, SStateAccelY, SStateAccelZ;


    //Parameters used for averaging filter
    private bool UseAvgAccel = true;
    public int AvgSize = 8;
    private float SumAccelX, SumAccelY, SumAccelZ;
    private float AvgAccelX, AvgAccelY, AvgAccelZ;

    //Parameter used for main update
    [Tooltip("Enable to display the displacement of the object")]
    public bool ShowDisp = true;
    [Tooltip("The scale factor to map real-world data into the Unity3D system")]
    public float DispScale = 10f;
    private Quaternion Rotate;
    private float QuatX, QuatY, QuatZ, QuatW;
    private float AccelX, AccelY, AccelZ;
    private float LastAccelX, LastAccelY, LastAccelZ;
    private float SpeedX, SpeedY, SpeedZ;
    private float LastSpeedX, LastSpeedY, LastSpeedZ;
    private float DispX, DispY, DispZ;
    private float LastDispX, LastDispY, LastDispZ;

    void Start()
    {
        if (RecordPlaybackRaw)
        {
            PlaybackFile = new FileInfo(@"..\FYP_Serial_Quat\Assets\Playback\PlaybackRaw.txt");
            SW = PlaybackFile.CreateText();
        }
        if (RecordPlayback)
        {
            PlaybackFile = new FileInfo(@"..\FYP_Serial_Quat\Assets\Playback\Playback.txt");
            SW = PlaybackFile.CreateText();
        }
        if (RecordTest)
        {
            PlaybackFile = new FileInfo(@"..\FYP_Serial_Quat\Assets\Playback\TestData.txt");
            SW = PlaybackFile.CreateText();
        }

        //Update Steady-state acceleration bias
        UpdataSStateAccel();

        //Initiate parameters
        OriginalPosition = this.transform.position;
        ResetParameters();

    }


    void Update()
    {

        // Run factory self test and calibration routine
        // Note: NO WORKING WITH nRF52840
        if (Input.GetKey(KeyCode.T))
        {

            GameObject.Find("RacketPivot").GetComponent<SerialController>().SendSerialMessage("t");

            //UpdataSStateAccel();
            //ResetParameters();

            this.transform.position = OriginalPosition;
        }

    }

    void FixedUpdate()
    {

        //Update the LastAccel
        LastAccelX = AvgAccelX;
        LastAccelY = AvgAccelY;
        LastAccelZ = AvgAccelZ;

        /*
        //Used for testing
        LastAccelX = AccelX;
        LastAccelY = AccelY;
        LastAccelZ = AccelZ;
        */

        LastSpeedX = SpeedX;
        LastSpeedY = SpeedY;
        LastSpeedZ = SpeedZ;

        LastDispX = DispX;
        LastDispY = DispY;
        LastDispZ = DispZ;

        //Read a serial message and update Quat and Accel data
        WholeLine = GameObject.Find("RacketPivot").GetComponent<SerialController>().ReadSerialMessage();

        if (WholeLine != null)
        {
            QAData = WholeLine.Split(new[] { ',' });

            if (QAData.Length != 7)
            {
                //Invalid input, use the same data as the last update
                Debug.Log("Invalid input, keep last valid data.");
            }
            else
            {
                //Valid input, update raw Quat and Accel data
                float.TryParse(QAData[0], out QuatW);
                float.TryParse(QAData[1], out QuatX);
                float.TryParse(QAData[2], out QuatY);
                float.TryParse(QAData[3], out QuatZ);

                float.TryParse(QAData[4], out AccelX);
                float.TryParse(QAData[5], out AccelY);
                float.TryParse(QAData[6], out AccelZ);

                //Subtract the steady-state bias
                AccelX = AccelX - SStateAccelX;
                AccelY = AccelY - SStateAccelY;
                AccelZ = AccelZ - SStateAccelZ;

                if (RecordPlaybackRaw)
                {
                    SW.WriteLine(WholeLine);
                }

            }
        }
        else
        {
            //Empty serial message, use the same data as the last update
            //Debug.Log("Empty serial message, keep last valid data");
        }

        //Update the average
        SumAccelX = AvgAccelX * (AvgSize - 1) + AccelX;
        AvgAccelX = SumAccelX / AvgSize;
        SumAccelY = AvgAccelY * (AvgSize - 1) + AccelY;
        AvgAccelY = SumAccelY / AvgSize;
        SumAccelZ = AvgAccelZ * (AvgSize - 1) + AccelZ;
        AvgAccelZ = SumAccelZ / AvgSize;

        //Zero acceleration threshold
        float MagnitudeAccel = Mathf.Sqrt(AvgAccelX * AvgAccelX + AvgAccelY * AvgAccelY + AvgAccelZ * AvgAccelZ);

        if (MagnitudeAccel < MagnitudeAccelThreshold)
        {
            ZeroAccelCount++;

            AvgAccelX = 0;
            AvgAccelY = 0;
            AvgAccelZ = 0;

        }

        if (ZeroAccelCount >= ZeroVelocityLimit)
        {
            SpeedX = 0;
            SpeedY = 0;
            SpeedZ = 0;

            DispX = 0;
            DispY = 0;
            DispZ = 0;

            ZeroAccelCount = 0;
        }
        else
        {
            //Apply the double integration
            SpeedX = LastSpeedX + LastAccelX * Time.fixedDeltaTime + (AvgAccelX - LastAccelX) * Time.fixedDeltaTime / 2f;
            SpeedY = LastSpeedY + LastAccelY * Time.fixedDeltaTime + (AvgAccelY - LastAccelY) * Time.fixedDeltaTime / 2f;
            SpeedZ = LastSpeedZ + LastAccelZ * Time.fixedDeltaTime + (AvgAccelZ - LastAccelZ) * Time.fixedDeltaTime / 2f;

            DispX = LastSpeedX * Time.fixedDeltaTime + (SpeedX - LastSpeedX) * Time.fixedDeltaTime / 2f;
            DispY = LastSpeedY * Time.fixedDeltaTime + (SpeedY - LastSpeedY) * Time.fixedDeltaTime / 2f;
            DispZ = LastSpeedZ * Time.fixedDeltaTime + (SpeedZ - LastSpeedZ) * Time.fixedDeltaTime / 2f;

        }
        
        /*
        {
            //Used for raw accel testing
            SpeedX = LastSpeedX + LastAccelX * Time.fixedDeltaTime + (AccelX - LastAccelX) * Time.fixedDeltaTime / 2f;
            SpeedY = LastSpeedY + LastAccelY * Time.fixedDeltaTime + (AccelY - LastAccelY) * Time.fixedDeltaTime / 2f;
            SpeedZ = LastSpeedZ + LastAccelZ * Time.fixedDeltaTime + (AccelZ - LastAccelZ) * Time.fixedDeltaTime / 2f;

            DispX = LastSpeedX * Time.fixedDeltaTime + (SpeedX - LastSpeedX) * Time.fixedDeltaTime / 2f;
            DispY = LastSpeedY * Time.fixedDeltaTime + (SpeedY - LastSpeedY) * Time.fixedDeltaTime / 2f;
            DispZ = LastSpeedZ * Time.fixedDeltaTime + (SpeedZ - LastSpeedZ) * Time.fixedDeltaTime / 2f;
        }
        */
        

        //Update the orientation state.
        //Nothe: different axis references are used in Unity3D and MPU9250.
        //For Unity3D: Quaternion(X,Y,Z,W).
        Rotate = new Quaternion(-QuatX, -QuatZ, -QuatY, QuatW);
        this.transform.rotation = Rotate;

        //Update the position state
        if (ShowDisp)
        {

            //this.transform.Translate(SpeedX, SpeedZ, SpeedY, Space.Self);
            
            this.transform.Translate(Vector3.right * DispScale *DispX);
            this.transform.Translate(Vector3.up * DispScale * DispZ);
            this.transform.Translate(Vector3.forward * DispScale * DispY);
            

        }

        if (RecordPlayback)
        {
            string RecordLine = QuatW.ToString() + "," + QuatX.ToString() + "," + QuatY.ToString() + "," + QuatZ.ToString() +
                                "," + SpeedX.ToString() + "," + SpeedY.ToString() + "," + SpeedZ.ToString();

            SW.WriteLine(RecordLine);
        }

        if (RecordTest)
        {
            WholeDispX += DispX;
            WholeDispY += DispY;
            WholeDispZ += DispZ;

            string RecordLine = AvgAccelX.ToString() + "," + AvgAccelY.ToString() + "," + AvgAccelZ.ToString() + "," +
                    SpeedX.ToString() + "," + SpeedY.ToString() + "," + SpeedZ.ToString() + "," + WholeDispX.ToString() + "," + WholeDispY.ToString() + ","
                    + WholeDispZ.ToString();
            SW.WriteLine(RecordLine);

        }

    }

    void UpdataSStateAccel()
    {

        int SStateCounter = 0;
        int LoopCounter = 0;

        float TempSumAccelX = 0;
        float TempSumAccelY = 0;
        float TempSumAccelZ = 0;

        while (SStateCounter < SStateLength)
        {

            WholeLine = GameObject.Find("RacketPivot").GetComponent<SerialController>().ReadSerialMessage();

            if (WholeLine != null)
            {

                //Debug.Log(WholeLine);
                QAData = WholeLine.Split(new[] { ',' });
                
                if (QAData.Length == 7)
                {

                    float.TryParse(QAData[4], out AccelX);
                    float.TryParse(QAData[5], out AccelY);
                    float.TryParse(QAData[6], out AccelZ);

                    TempSumAccelX += AccelX;
                    TempSumAccelY += AccelY;
                    TempSumAccelZ += AccelZ;

                    SStateCounter++;

                }

            }

            LoopCounter++;
            if (MaxLoopControl && (LoopCounter >= MaxLoopCount))
            {
                break;
            }

        }

        if (MaxLoopControl && (LoopCounter >= MaxLoopCount))
        {
            Debug.LogWarning("Loop counter exceed limit, number of valid input for SState: " + SStateCounter.ToString());
        }

        SStateAccelX = TempSumAccelX / SStateCounter;
        SStateAccelY = TempSumAccelY / SStateCounter;
        SStateAccelZ = TempSumAccelZ / SStateCounter;

    }

    void ResetParameters()
    {

        this.transform.position = OriginalPosition;

        ZeroAccelCount = 0;

        SpeedX = 0;
        SpeedY = 0;
        SpeedZ = 0;

        DispX = 0;
        DispY = 0;
        DispZ = 0;

        LastAccelX = 0;
        LastAccelY = 0;
        LastAccelZ = 0;

        LastSpeedX = 0;
        LastSpeedY = 0;
        LastSpeedZ = 0;

        LastDispX = 0;
        LastDispY = 0;
        LastDispZ = 0;

        if (UseAvgAccel) {

            SumAccelX = 0;
            SumAccelY = 0;
            SumAccelZ = 0;

            //Initiate the AvgAccel   
            int AvgCount = 0;
            int LoopCounter = 0;

            while (AvgCount < AvgSize)
            {

                WholeLine = GameObject.Find("RacketPivot").GetComponent<SerialController>().ReadSerialMessage();

                if (WholeLine != null)
                {

                    QAData = WholeLine.Split(new[] { ',' });

                    if (QAData.Length == 7)
                    {

                        float.TryParse(QAData[4], out AccelX);
                        float.TryParse(QAData[5], out AccelY);
                        float.TryParse(QAData[6], out AccelZ);

                        SumAccelX += AccelX;
                        SumAccelY += AccelY;
                        SumAccelZ += AccelZ;

                        AvgCount++;

                    }

                }

                LoopCounter++;
                if (MaxLoopControl && (LoopCounter > MaxLoopCount))
                {
                    break;
                }

            }

            if (MaxLoopControl && (LoopCounter > MaxLoopCount))
            {
                Debug.LogWarning("Loop counter exceed limit, number of valid input for Avg: " + AvgCount.ToString());
            }

            AvgAccelX = SumAccelX / AvgCount;
            AvgAccelY = SumAccelY / AvgCount;
            AvgAccelZ = SumAccelZ / AvgCount;

        }

    }

    void OnDisable()
    {
        SW.Close();
    }

}