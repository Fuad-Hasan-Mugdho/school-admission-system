import { ApiError } from './client';

/**
 * Turn any failure into a sentence a parent or school clerk can act on.
 * Bangla is the primary language of the UI; the server's English message is used only as a last resort.
 */
export function describeError(error: unknown): string {
  if (error instanceof ApiError) {
    switch (error.code) {
      case 'NETWORK':
        return 'Server-এর সাথে সংযোগ করা যাচ্ছে না। Internet বা server চালু আছে কি না দেখে আবার চেষ্টা করুন।';
      case 'SEAT_UNAVAILABLE':
        return 'দুঃখিত, এই class-এ এখন কোনো seat খালি নেই।';
      case 'ALREADY_APPLIED':
        return 'এই শিক্ষার্থীর এই class-এ ইতিমধ্যে একটা চলমান আবেদন বা নিশ্চিত ভর্তি আছে।';
      case 'HOLD_EXPIRED':
        return 'এই আবেদনের মেয়াদ শেষ হয়ে গেছে, seat-টি ছেড়ে দেওয়া হয়েছে। চাইলে আবার আবেদন করুন।';
      case 'HOLD_CANCELLED':
        return 'এই আবেদনটি বাতিল করা হয়েছে, তাই আর ফি জমা দেওয়া যাবে না।';
      case 'HOLD_CONFIRMED':
        return 'এই আবেদনের ভর্তি ইতিমধ্যে নিশ্চিত হয়ে গেছে, এখান থেকে বাতিল করা যাবে না।';
      case 'NOT_OWNER':
        return 'এই আবেদনটি অন্য একজন শিক্ষার্থীর। উপরে সঠিক জন্ম নিবন্ধন নম্বর দিন।';
      case 'CLASS_NOT_FOUND':
        return 'এই class-টি খুঁজে পাওয়া যায়নি। হয়তো তালিকা পুরোনো হয়ে গেছে।';
      case 'HOLD_NOT_FOUND':
        return 'এই আবেদনটি খুঁজে পাওয়া যায়নি।';
      case 'STUDENT_ID_REQUIRED':
        return 'শিক্ষার্থীর ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।';
      case 'VALIDATION_ERROR': {
        const details = error.details ? Object.values(error.details).join(' ') : '';
        return `দেওয়া তথ্য সঠিক নয়। ${details || error.message}`.trim();
      }
      case 'INTERNAL_ERROR':
        return 'Server-এ একটা সমস্যা হয়েছে। একটু পরে আবার চেষ্টা করুন।';
    }
    if (error.status >= 500) return 'Server-এ একটা সমস্যা হয়েছে। একটু পরে আবার চেষ্টা করুন।';
    if (error.status === 404) return 'যা খুঁজছেন তা পাওয়া যায়নি।';
    return error.message || 'অনুরোধটি সম্পন্ন করা যায়নি।';
  }
  if (error instanceof Error && error.message) {
    return `একটা সমস্যা হয়েছে: ${error.message}`;
  }
  return 'অজানা একটা সমস্যা হয়েছে। আবার চেষ্টা করুন।';
}
