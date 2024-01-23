﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Terminal.Gui; 

class DictionaryJsonConverter<T> : JsonConverter<Dictionary<string, T>> {
	public override Dictionary<string, T> Read (ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartArray) {
			throw new JsonException ($"Expected a JSON array (\"[ {{ ... }} ]\"), but got \"{reader.TokenType}\".");
		}

		var dictionary = new Dictionary<string, T> ();
		while (reader.Read ()) {
			if (reader.TokenType == JsonTokenType.StartObject) {
				reader.Read ();
				if (reader.TokenType == JsonTokenType.PropertyName) {
					var key = reader.GetString ();
					reader.Read ();
					var value = JsonSerializer.Deserialize<T> (ref reader, options);
					dictionary.Add (key, value);
				}
			} else if (reader.TokenType == JsonTokenType.EndArray) {
				break;
			}
		}
		return dictionary;
	}

	public override void Write (Utf8JsonWriter writer, Dictionary<string, T> value, JsonSerializerOptions options)
	{
		writer.WriteStartArray ();
		foreach (var item in value) {
			writer.WriteStartObject ();
			//writer.WriteString (item.Key, item.Key);
			writer.WritePropertyName (item.Key);
			JsonSerializer.Serialize (writer, item.Value, options);
			writer.WriteEndObject ();
		}
		writer.WriteEndArray ();
	}
}