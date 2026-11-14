namespace Polhem.AI.Claude
{
    /// <summary>
    /// Why a request to Claude produced no usable answer.
    /// </summary>
    public enum ClaudeFailureKind
    {
        /// <summary>The model, or its safety classifiers, declined the request, and no fallback model answered it.</summary>
        Refused,

        /// <summary>The answer reached the output token limit and was cut short.</summary>
        OutputLimitReached,

        /// <summary>The API did not accept the credentials: the sign-in expired, or the API key is wrong.</summary>
        NotAuthenticated,
    }
}
