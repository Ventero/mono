//
// Copyright (C) 2010 Novell Inc. http://novell.com
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

/*

* State transition

Unlike XmlWriter, XAML nodes are not immediately writable because object
output has to be delayed to be determined whether it should write
an attribute or an element.

** NamespaceDeclarations

NamespaceDeclaration does not immediately participate in the state transition
but some write methods reject stored namespaces (e.g. WriteEndObject cannot
handle them). In such cases, they throw InvalidOperationException, while the
writer throws XamlXmlWriterException for usual state transition.

Though they still seems to affect some outputs. If a member with simple
value is written after a namespace, then it becomes an element, not attribute.

** state transition

states are: Initial, ObjectStarted, MemberStarted, ValueWritten, MemberDone, End

Initial + StartObject -> ObjectStarted : push(xt)
ObjectStarted + StartMember -> MemberStarted : push(xm)
ObjectStarted + EndObject -> ObjectWritten or End : pop()
MemberStarted + StartObject -> ObjectStarted : push(xt)
MemberStarted + Value -> ValueWritten
MemberStarted + GetObject -> MemberDone : pop()
ObjectWritten + StartObject -> ObjectStarted : push(x)
ObjectWritten + Value -> ValueWritten : pop()
ObjectWritten + EndMember -> MemberDone : pop()
ValueWritten + StartObject -> ObjectStarted : push(x)
ValueWritten + EndMember -> MemberDone : pop()
MemberDone + EndObject -> ObjectWritten or End : pop() // xt
MemberDone + StartMember -> MemberStarted : push(xm)


*/

namespace System.Xaml
{
	internal class XamlWriterStateManager
	{
		enum XamlWriteState
		{
			Initial,
			ObjectStarted,
			MemberStarted,
			ObjectWritten,
			ValueWritten,
			MemberDone,
			End
		}

		XamlWriteState state = XamlWriteState.Initial;
		bool ns_pushed;

		public void OnClosingItem ()
		{
			// somewhat hacky state change to not reject StartMember->EndMember.
			if (state == XamlWriteState.MemberStarted)
				state = XamlWriteState.ValueWritten;
		}

		public void EndMember ()
		{
			RejectNamespaces (XamlNodeType.EndMember);
			CheckState (XamlNodeType.EndMember);
			state = XamlWriteState.MemberDone;
		}

		public void EndObject (bool hasMoreNodes)
		{
			RejectNamespaces (XamlNodeType.EndObject);
			CheckState (XamlNodeType.EndObject);
			state = hasMoreNodes ? XamlWriteState.ObjectWritten : XamlWriteState.End;
		}

		public void GetObject ()
		{
			CheckState (XamlNodeType.GetObject);
			RejectNamespaces (XamlNodeType.GetObject);
			state = XamlWriteState.MemberDone;
		}

		public void StartMember ()
		{
			CheckState (XamlNodeType.StartMember);
			state = XamlWriteState.MemberStarted;
			ns_pushed = false;
		}

		public void StartObject ()
		{
			CheckState (XamlNodeType.StartObject);
			state = XamlWriteState.ObjectStarted;
			ns_pushed = false;
		}

		public void Value ()
		{
			CheckState (XamlNodeType.Value);
			RejectNamespaces (XamlNodeType.Value);
			state = XamlWriteState.ValueWritten;
		}

		public void Namespace ()
		{
			ns_pushed = true;
		}

		public void NamespaceCleanedUp ()
		{
			ns_pushed = false;
		}

		void CheckState (XamlNodeType next)
		{
			switch (state) {
			case XamlWriteState.Initial:
				switch (next) {
				case XamlNodeType.StartObject:
					return;
				}
				break;
			case XamlWriteState.ObjectStarted:
				switch (next) {
				case XamlNodeType.StartMember:
				case XamlNodeType.EndObject:
					return;
				}
				break;
			case XamlWriteState.MemberStarted:
				switch (next) {
				case XamlNodeType.StartObject:
				case XamlNodeType.Value:
				case XamlNodeType.GetObject:
					return;
				}
				break;
			case XamlWriteState.ObjectWritten:
				switch (next) {
				case XamlNodeType.StartObject:
				case XamlNodeType.Value:
				case XamlNodeType.EndMember:
					return;
				}
				break;
			case XamlWriteState.ValueWritten:
				switch (next) {
				case XamlNodeType.StartObject:
				case XamlNodeType.EndMember:
					return;
				}
				break;
			case XamlWriteState.MemberDone:
				switch (next) {
				case XamlNodeType.StartMember:
				case XamlNodeType.EndObject:
					return;
				}
				break;
			}
			throw new XamlXmlWriterException (String.Format ("{0} is not allowed at current state {1}", next, state));
		}
		
		void RejectNamespaces (XamlNodeType next)
		{
			if (ns_pushed) {
				// strange, but on WriteEndMember it throws XamlXmlWriterException, while for other nodes it throws IOE.
				string msg = String.Format ("Namespace declarations cannot be written before {0}", next);
				if (next == XamlNodeType.EndMember)
					throw new XamlXmlWriterException (msg);
				else
					throw new InvalidOperationException (msg);
			}
		}
	}
}
