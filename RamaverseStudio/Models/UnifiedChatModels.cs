using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RamaverseStudio.Models
{
    public enum PlatformType
    {
        Twitch,
        Kick,
        YouTube,
        Streamlabs,
        StreamElements,
        System
    }

    public enum RoleBadgeType
    {
        Broadcaster,
        Moderator,
        VIP,
        Subscriber,
        Verified,
        Founder,
        Custom
    }

    public class ChatBadge
    {
        public RoleBadgeType BadgeType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public BitmapSource? CachedImage { get; set; }
        public SolidColorBrush FallbackColor { get; set; } = Brushes.Gray;
    }

    public enum ChatElementType
    {
        Text,
        Emote,
        Mention,
        Url
    }

    public class ChatContentElement
    {
        public ChatElementType Type { get; set; } = ChatElementType.Text;
        public string Text { get; set; } = string.Empty;
        public string? EmoteId { get; set; }
        public string? EmoteUrl { get; set; }
        public BitmapSource? CachedEmoteBitmap { get; set; }
        public bool IsAnimated { get; set; }
    }

    public class UnifiedChatMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public PlatformType Platform { get; set; } = PlatformType.Twitch;
        public string ChannelName { get; set; } = string.Empty;
        public string SenderId { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string SenderColorHex { get; set; } = "#FFFFFF";
        public SolidColorBrush SenderBrush { get; set; } = Brushes.White;
        public string RawMessage { get; set; } = string.Empty;
        public List<ChatContentElement> Elements { get; set; } = new();
        public List<ChatBadge> Badges { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string FormattedTime => Timestamp.ToString("HH:mm:ss");

        public bool IsBroadcaster { get; set; }
        public bool IsModerator { get; set; }
        public bool IsVIP { get; set; }
        public bool IsSubscriber { get; set; }
        public int SubMonths { get; set; }

        public bool IsDonation { get; set; }
        public string DonationAmount { get; set; } = string.Empty;
        public SolidColorBrush? HighlightBackground { get; set; }
    }

    public enum StreamAlertKind
    {
        Follow,
        Subscription,
        Resubscription,
        GiftSubscription,
        Raid,
        BitsCheer,
        DonationTip,
        SuperChat
    }

    public class UnifiedAlertEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public PlatformType Platform { get; set; }
        public StreamAlertKind Kind { get; set; }
        public string Username { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string AmountOrTier { get; set; } = string.Empty;
        public string UserMessage { get; set; } = string.Empty;
        public string FormattedHeadline { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
