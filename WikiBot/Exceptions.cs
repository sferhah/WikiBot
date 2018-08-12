using System;

namespace WikiBot
{
    /// <summary>Class establishes custom application exceptions.</summary>
    /// <exclude/>
    public class WikiBotException : Exception
    {
        /// <exclude/>
        public WikiBotException() { }
        /// <exclude/>
        public WikiBotException(string msg) : base(msg)
        {
        }
        /// <exclude/>
        public WikiBotException(string msg, Exception inner) : base(msg, inner) { }
        /// <exclude/>
        protected WikiBotException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
        /// <exclude/>
        
    }
    /// <summary>Exceptions for handling wiki edit conflicts.</summary>
    /// <exclude/>
    public class EditConflictException : WikiBotException
    {
        /// <exclude/>
        public EditConflictException() { }
        /// <exclude/>
        public EditConflictException(string msg) : base(msg) { }
        /// <exclude/>
        public EditConflictException(string msg, Exception inner) : base(msg, inner) { }
    }

    /// <summary>Exception for handling errors due to insufficient rights.</summary>
    /// <exclude/>
    public class InsufficientRightsException : WikiBotException
    {
        /// <exclude/>
        public InsufficientRightsException() { }
        /// <exclude/>
        public InsufficientRightsException(string msg) : base(msg) { }
        /// <exclude/>
        public InsufficientRightsException(string msg, Exception inner) : base(msg, inner) { }
    }

    /// <summary>Exception for handling situations when bot operation is disallowed.</summary>
    /// <exclude/>
    public class BotDisallowedException : WikiBotException
    {
        /// <exclude/>
        public BotDisallowedException() { }
        /// <exclude/>
        public BotDisallowedException(string msg) : base(msg) { }
        /// <exclude/>
        public BotDisallowedException(string msg, Exception inner) : base(msg, inner) { }
    }

}
