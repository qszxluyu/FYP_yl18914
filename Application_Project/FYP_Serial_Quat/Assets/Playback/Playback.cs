using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Playback : MonoBehaviour {

    public bool PlaybackRaw;
    public bool ShowDisp;
    public string PlaybackLine;
    private StreamReader SR;
    private string[] QAData;
    public float QuatX, QuatY, QuatZ, QuatW;
    private Quaternion Rotate;
    public float SpeedX, SpeedY, SpeedZ;




	// Use this for initialization
	void Start ()
    {
   
        if (PlaybackRaw)
        {
            // PlaybackLines = System.IO.File.ReadAllLines(@"..\FYP_Serial_Quat\Assets\Playback\PlaybackRaw.txt");
            SR = new StreamReader(@"..\FYP_Serial_Quat\Assets\Playback\PlaybackRaw.txt");
        }
        else
        {
            //PlaybackLines = System.IO.File.ReadAllLines(@"..\FYP_Serial_Quat\Assets\Playback\Playback.txt");
            SR = new StreamReader(@"..\FYP_Serial_Quat\Assets\Playback\Playback.txt");
        }

    }

    // Update is called once per frame
    void Update ()
    {
		//Do nothing
	}

    private void FixedUpdate()
    {
        if (PlaybackRaw)
        {
            PlaybackRawUpdate();
        }
        else
        {
            PlaybackUpdate();
        }
    }

    private void PlaybackRawUpdate()
    {
        Debug.LogWarning("Have no implement yet");
    }

    private void PlaybackUpdate()
    {
        PlaybackLine = SR.ReadLine();
        if (PlaybackLine != null)
        {
            QAData = PlaybackLine.Split(new[] { ',' });

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

                float.TryParse(QAData[4], out SpeedX);
                float.TryParse(QAData[5], out SpeedY);
                float.TryParse(QAData[6], out SpeedZ);

            }
        }
        else
        {
            //Empty serial message, use the same data as the last update
            Debug.Log("Empty serial message, keep last valid data");
        }

        Rotate = new Quaternion(-QuatX, -QuatZ, -QuatY, QuatW);

        //Apply the transform

        if (ShowDisp)
        {

            this.transform.Translate(SpeedX, SpeedZ, SpeedY, Space.Self);
            /*
            this.transform.Translate(Vector3.right * DispX);
            this.transform.Translate(Vector3.up * DispZ);
            this.transform.Translate(Vector3.forward * DispY);
            */

        }

        this.transform.rotation = Rotate;

        if (SR.EndOfStream)
        {
            Debug.Log("Playback ended");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
Application.Quit();
#endif
        }
    }

    private void OnDisable()
    {
        SR.Close();
    }
}
