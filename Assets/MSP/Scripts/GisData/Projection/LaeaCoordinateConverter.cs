using System;
using System.Globalization;

namespace MSP.Scripts.GisData
{
	/// <summary>
	/// Ellipsoidal Lambert azimuthal equal-area projection, oblique aspect (PROJ
	/// "+proj=laea"), following Snyder, "Map Projections - A Working Manual" (1987),
	/// section 24. Longitude/latitude are taken to be on the projection's ellipsoid
	/// (no datum shift, which is also what PROJ does without +towgs84; WGS84 and
	/// GRS80 differ by well under a millimetre).
	///
	/// The polar aspect (|lat_0| = 90) is not implemented. The inverse uses Snyder's
	/// series for the authalic -> geodetic latitude, accurate to about a centimetre,
	/// which is plenty for round-trip checks.
	/// </summary>
	public class LaeaCoordinateConverter : ICoordinateConverter
	{
		private const double Deg2Rad = Math.PI / 180.0;
		private const double Rad2Deg = 180.0 / Math.PI;

		private readonly double m_semiMajorAxis;
		private readonly double m_flattening;
		private readonly double m_lat0Degrees;
		private readonly double m_lon0Degrees;
		private readonly double m_lat0;
		private readonly double m_lon0;
		private readonly double m_falseEasting;
		private readonly double m_falseNorthing;
		private readonly double m_e;
		private readonly double m_e2;
		private readonly double m_qp;
		private readonly double m_rq;
		private readonly double m_d;
		private readonly double m_sinBeta1;
		private readonly double m_cosBeta1;

		/// <param name="a_semiMajorAxis">Ellipsoid semi-major axis, metres.</param>
		/// <param name="a_flattening">Ellipsoid flattening (1 / inverse flattening).</param>
		/// <param name="a_lat0Degrees">Latitude of origin.</param>
		/// <param name="a_lon0Degrees">Longitude of origin.</param>
		public LaeaCoordinateConverter(double a_semiMajorAxis, double a_flattening, double a_lat0Degrees, double a_lon0Degrees, double a_falseEasting, double a_falseNorthing)
		{
			if (Math.Abs(a_lat0Degrees) >= 89.999999)
				throw new NotSupportedException("The polar aspect of laea (lat_0 = +-90) is not supported.");

			m_semiMajorAxis = a_semiMajorAxis;
			m_flattening = a_flattening;
			m_lat0Degrees = a_lat0Degrees;
			m_lon0Degrees = a_lon0Degrees;
			m_e2 = a_flattening * (2.0 - a_flattening);
			m_e = Math.Sqrt(m_e2);
			m_lat0 = a_lat0Degrees * Deg2Rad;
			m_lon0 = a_lon0Degrees * Deg2Rad;
			m_falseEasting = a_falseEasting;
			m_falseNorthing = a_falseNorthing;

			double phi1 = m_lat0;
			m_qp = AuthalicQ(Math.PI / 2.0);
			double beta1 = Math.Asin(AuthalicQ(phi1) / m_qp);
			m_sinBeta1 = Math.Sin(beta1);
			m_cosBeta1 = Math.Cos(beta1);
			m_rq = a_semiMajorAxis * Math.Sqrt(m_qp / 2.0);

			double sinPhi1 = Math.Sin(phi1);
			m_d = a_semiMajorAxis * (Math.Cos(phi1) / Math.Sqrt(1.0 - m_e2 * sinPhi1 * sinPhi1)) / (m_rq * m_cosBeta1);
		}

		public void FromWgs84(double a_lon, double a_lat, out double a_x, out double a_y)
		{
			double phi = a_lat * Deg2Rad;
			double deltaLon = NormaliseRadians(a_lon * Deg2Rad - m_lon0);

			double beta = Math.Asin(Math.Max(-1.0, Math.Min(1.0, AuthalicQ(phi) / m_qp)));
			double sinBeta = Math.Sin(beta);
			double cosBeta = Math.Cos(beta);
			double cosDeltaLon = Math.Cos(deltaLon);

			double denominator = 1.0 + m_sinBeta1 * sinBeta + m_cosBeta1 * cosBeta * cosDeltaLon;
			if (denominator < 1e-12)
				throw new ArgumentOutOfRangeException(nameof(a_lat), "Point is the antipode of the projection origin and cannot be projected.");

			double b = m_rq * Math.Sqrt(2.0 / denominator);
			a_x = m_falseEasting + b * m_d * cosBeta * Math.Sin(deltaLon);
			a_y = m_falseNorthing + (b / m_d) * (m_cosBeta1 * sinBeta - m_sinBeta1 * cosBeta * cosDeltaLon);
		}

		public void ToWgs84(double a_x, double a_y, out double a_lon, out double a_lat)
		{
			double x = a_x - m_falseEasting;
			double y = a_y - m_falseNorthing;

			double rho = Math.Sqrt((x / m_d) * (x / m_d) + (m_d * y) * (m_d * y));
			if (rho < 1e-9)
			{
				a_lon = m_lon0 * Rad2Deg;
				a_lat = m_lat0 * Rad2Deg;
				return;
			}

			double ce = 2.0 * Math.Asin(Math.Max(-1.0, Math.Min(1.0, rho / (2.0 * m_rq))));
			double sinCe = Math.Sin(ce);
			double cosCe = Math.Cos(ce);

			double beta = Math.Asin(Math.Max(-1.0, Math.Min(1.0, cosCe * m_sinBeta1 + (m_d * y * sinCe * m_cosBeta1) / rho)));
			double deltaLon = Math.Atan2(x * sinCe, m_d * rho * m_cosBeta1 * cosCe - m_d * m_d * y * m_sinBeta1 * sinCe);

			a_lon = NormaliseRadians(m_lon0 + deltaLon) * Rad2Deg;
			a_lat = AuthalicToGeodetic(beta) * Rad2Deg;
		}

		public string ToWkt()
		{
			CultureInfo c = CultureInfo.InvariantCulture;
			return "PROJCS[\"MSP dataset projection (LAEA)\"," +
				"GEOGCS[\"Geographic CRS on the dataset ellipsoid\"," +
				$"DATUM[\"Dataset datum\",SPHEROID[\"Dataset ellipsoid\",{m_semiMajorAxis.ToString("R", c)},{(1.0 / m_flattening).ToString("R", c)}]]," +
				"PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]]," +
				"PROJECTION[\"Lambert_Azimuthal_Equal_Area\"]," +
				$"PARAMETER[\"latitude_of_center\",{m_lat0Degrees.ToString("R", c)}]," +
				$"PARAMETER[\"longitude_of_center\",{m_lon0Degrees.ToString("R", c)}]," +
				$"PARAMETER[\"false_easting\",{m_falseEasting.ToString("R", c)}]," +
				$"PARAMETER[\"false_northing\",{m_falseNorthing.ToString("R", c)}]," +
				"UNIT[\"metre\",1]]";
		}

		//Snyder 3-12: q for geodetic latitude phi
		private double AuthalicQ(double a_phi)
		{
			double sinPhi = Math.Sin(a_phi);
			double eSinPhi = m_e * sinPhi;
			return (1.0 - m_e2) * (sinPhi / (1.0 - eSinPhi * eSinPhi) - (1.0 / (2.0 * m_e)) * Math.Log((1.0 - eSinPhi) / (1.0 + eSinPhi)));
		}

		//Snyder 3-18: authalic latitude beta -> geodetic latitude phi
		private double AuthalicToGeodetic(double a_beta)
		{
			double e4 = m_e2 * m_e2;
			double e6 = e4 * m_e2;
			return a_beta
				+ (m_e2 / 3.0 + 31.0 * e4 / 180.0 + 517.0 * e6 / 5040.0) * Math.Sin(2.0 * a_beta)
				+ (23.0 * e4 / 360.0 + 251.0 * e6 / 3780.0) * Math.Sin(4.0 * a_beta)
				+ (761.0 * e6 / 45360.0) * Math.Sin(6.0 * a_beta);
		}

		private static double NormaliseRadians(double a_radians)
		{
			while (a_radians > Math.PI)
				a_radians -= 2.0 * Math.PI;
			while (a_radians < -Math.PI)
				a_radians += 2.0 * Math.PI;
			return a_radians;
		}
	}
}
