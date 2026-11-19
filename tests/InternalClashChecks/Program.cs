using System;
using System.Collections.Generic;
using RevitFamilyImporter.InternalClash;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
var empty = new HashSet<int>();
Check(!InternalClashPairPolicy.ShouldCompare(1, 1, 1, 1), "O próprio elemento não deve ser comparado.");
Check(InternalClashPairPolicy.ShouldCompare(1, 2, 1, 2) && !InternalClashPairPolicy.ShouldCompare(2, 1, 2, 1), "Cada par deve ser comparado uma vez.");
Check(!InternalClashPairPolicy.ShouldCompare(1, 2, 9, 9), "Componentes da mesma família não devem gerar conflito interno.");
Check(InternalClashPairPolicy.IsConnected(1, 2, 10, 20, new HashSet<int> { 2 }, empty), "Conexão direta deve ignorar o par.");
Check(InternalClashPairPolicy.IsConnected(1, 2, 10, 20, empty, new HashSet<int> { 10 }), "Conexão com proprietário deve ser reconhecida nos dois sentidos.");
Check(!InternalClashPairPolicy.IsConnected(1, 2, 10, 20, new HashSet<int> { 3 }, empty), "Conexão com terceiro não deve ignorar o par.");
Check(InternalClashPairPolicy.CompletionKey("primeiro", "segundo") == InternalClashPairPolicy.CompletionKey("segundo", "primeiro"), "A memória deve identificar o par independente da ordem.");
Console.WriteLine("Pares únicos, componentes da família e conexões verificados.");
