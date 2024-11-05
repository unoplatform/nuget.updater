﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NvGet.Tools.Arguments
{
	public class ConsoleArgError
	{
		public ConsoleArgErrorType Type { get; set; }

		public string Argument { get; set; }

		public Exception Exception { get; set; }

		public ConsoleArgError(string argument, ConsoleArgErrorType type, Exception e = null)
		{
			Argument = argument;
			Type = type;
			Exception = e;
		}

		public string Message => Type switch
		{
			ConsoleArgErrorType.UnrecognizedArgument => "unrecognized argument: " + Argument,
			ConsoleArgErrorType.ValueAssignmentError => $"error while trying to assign value: {Argument} (Exception: {Exception?.Message ?? "None"})",
			ConsoleArgErrorType.ValueParsingError => $"error while trying to parse value: {Argument} (Exception: {Exception?.Message ?? "None"})",

			_ => $"{Type}: " + Argument,
		};
	}
}
