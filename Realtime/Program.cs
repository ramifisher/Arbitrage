using System;

using SmartQuant;

namespace OpenQuant
{
	class Program
	{
		static void Main(string[] args)
		{
			Scenario scenario = new Realtime(Framework.Current);

			scenario.Run();
		}
	}
}
