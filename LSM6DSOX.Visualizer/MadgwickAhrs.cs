using System;

namespace LSM6DSOX.Visualizer;

/// <summary>
/// Madgwicks IMU-algoritm (accelerometer + gyroskop, ingen magnetometer).
/// Tar emot gyrodata i grader/sekund och accelerometerdata i valfri enhet
/// (normaliseras internt), och underhåller en orienteringskvaternion.
/// </summary>
public class MadgwickAhrs
{
    private readonly double _sampleFreqHz;
    public double Beta { get; set; } = 0.1;

    public double Q0 { get; private set; } = 1.0;
    public double Q1 { get; private set; } = 0.0;
    public double Q2 { get; private set; } = 0.0;
    public double Q3 { get; private set; } = 0.0;

    public MadgwickAhrs(double sampleFreqHz)
    {
        _sampleFreqHz = sampleFreqHz;
    }

    /// <summary>
    /// Uppdaterar orienteringen med ett nytt sensorsampel.
    /// </summary>
    /// <param name="gxDps">Gyro X i grader/sekund</param>
    /// <param name="gyDps">Gyro Y i grader/sekund</param>
    /// <param name="gzDps">Gyro Z i grader/sekund</param>
    /// <param name="ax">Accel X (valfri konsekvent enhet, t.ex. g)</param>
    /// <param name="ay">Accel Y</param>
    /// <param name="az">Accel Z</param>
    public void UpdateImu(double gxDps, double gyDps, double gzDps, double ax, double ay, double az)
    {
        double gx = gxDps * Math.PI / 180.0;
        double gy = gyDps * Math.PI / 180.0;
        double gz = gzDps * Math.PI / 180.0;

        double q0 = Q0, q1 = Q1, q2 = Q2, q3 = Q3;

        double qDot1 = 0.5 * (-q1 * gx - q2 * gy - q3 * gz);
        double qDot2 = 0.5 * (q0 * gx + q2 * gz - q3 * gy);
        double qDot3 = 0.5 * (q0 * gy - q1 * gz + q3 * gx);
        double qDot4 = 0.5 * (q0 * gz + q1 * gy - q2 * gx);

        if (!(ax == 0.0 && ay == 0.0 && az == 0.0))
        {
            double recipNorm = 1.0 / Math.Sqrt(ax * ax + ay * ay + az * az);
            ax *= recipNorm; ay *= recipNorm; az *= recipNorm;

            double _2q0 = 2.0 * q0, _2q1 = 2.0 * q1, _2q2 = 2.0 * q2, _2q3 = 2.0 * q3;
            double _4q0 = 4.0 * q0, _4q1 = 4.0 * q1, _4q2 = 4.0 * q2;
            double _8q1 = 8.0 * q1, _8q2 = 8.0 * q2;
            double q0q0 = q0 * q0, q1q1 = q1 * q1, q2q2 = q2 * q2, q3q3 = q3 * q3;

            double s0 = _4q0 * q2q2 + _2q2 * ax + _4q0 * q1q1 - _2q1 * ay;
            double s1 = _4q1 * q3q3 - _2q3 * ax + 4.0 * q0q0 * q1 - _2q0 * ay - _4q1 + _8q1 * q1q1 + _8q1 * q2q2 + _4q1 * az;
            double s2 = 4.0 * q0q0 * q2 + _2q0 * ax + _4q2 * q3q3 - _2q3 * ay - _4q2 + _8q2 * q1q1 + _8q2 * q2q2 + _4q2 * az;
            double s3 = 4.0 * q1q1 * q3 - _2q1 * ax + 4.0 * q2q2 * q3 - _2q2 * ay;

            recipNorm = 1.0 / Math.Sqrt(s0 * s0 + s1 * s1 + s2 * s2 + s3 * s3);
            s0 *= recipNorm; s1 *= recipNorm; s2 *= recipNorm; s3 *= recipNorm;

            qDot1 -= Beta * s0;
            qDot2 -= Beta * s1;
            qDot3 -= Beta * s2;
            qDot4 -= Beta * s3;
        }

        q0 += qDot1 * (1.0 / _sampleFreqHz);
        q1 += qDot2 * (1.0 / _sampleFreqHz);
        q2 += qDot3 * (1.0 / _sampleFreqHz);
        q3 += qDot4 * (1.0 / _sampleFreqHz);

        double norm = 1.0 / Math.Sqrt(q0 * q0 + q1 * q1 + q2 * q2 + q3 * q3);
        Q0 = q0 * norm;
        Q1 = q1 * norm;
        Q2 = q2 * norm;
        Q3 = q3 * norm;
    }
}
