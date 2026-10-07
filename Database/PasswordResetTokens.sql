CREATE TABLE IF NOT EXISTS public.PasswordResetTokens
(
    Id uuid PRIMARY KEY,
    UserId uuid NOT NULL REFERENCES public.Users(Id) ON DELETE CASCADE,
    TokenHash varchar(64) NOT NULL,
    ExpiresAt timestamptz NOT NULL,
    Used boolean NOT NULL DEFAULT FALSE,
    CreatedAt timestamptz NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_PasswordResetTokens_TokenHash
    ON public.PasswordResetTokens(TokenHash);

CREATE INDEX IF NOT EXISTS IX_PasswordResetTokens_UserId
    ON public.PasswordResetTokens(UserId);
