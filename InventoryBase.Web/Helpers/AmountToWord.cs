namespace InventoryBase.Web.Helpers;

public static class AmountToWord
{
    public static string ToWords(decimal amount)
    {
        long taka  = (long)Math.Floor(amount);
        int  paisa = (int)Math.Round((amount - taka) * 100);

        string takaWords  = ConvertWholeNumber(taka.ToString());
        if (string.IsNullOrWhiteSpace(takaWords)) takaWords = "Zero";

        string result = $"Taka {takaWords}";
        if (paisa > 0)
            result += $" and {ConvertWholeNumber(paisa.ToString())} Paisa";
        return result + " Only";
    }

    private static string ConvertWholeNumber(string number)
    {
        string word = "";
        try
        {
            double dblAmt = Convert.ToDouble(number);
            if (dblAmt <= 0) return "";

            bool beginsZero = number.StartsWith("0");
            bool isDone     = false;
            int  numDigits  = beginsZero ? dblAmt.ToString().Length : number.Length;
            int  pos        = 0;
            string place    = "";

            switch (numDigits)
            {
                case 1: word = Ones(number); isDone = true; break;
                case 2: word = Tens(number); isDone = true; break;
                case 3: pos = (numDigits % 3) + 1; place = " Hundred ";  break;
                case 4:
                case 5: pos = (numDigits % 4) + 1; place = " Thousand "; break;
                case 6:
                case 7:
                case 8:
                case 9: pos = (numDigits % 6) + 1; place = " Lac ";      break;
                case 10: pos = (numDigits % 10) + 1; place = " Billion "; break;
                default: isDone = true; break;
            }

            if (!isDone)
            {
                word = ConvertWholeNumber(number[..pos]) + place + ConvertWholeNumber(number[pos..]);
                if (beginsZero) word = " and " + word.Trim();
            }

            if (word.Trim().Equals(place.Trim(), StringComparison.OrdinalIgnoreCase))
                word = "";
        }
        catch { }
        return word.Trim();
    }

    private static string Ones(string digit)
    {
        return Convert.ToInt32(digit) switch
        {
            1 => "One",   2 => "Two",   3 => "Three", 4 => "Four",
            5 => "Five",  6 => "Six",   7 => "Seven", 8 => "Eight",
            9 => "Nine",  _ => ""
        };
    }

    private static string Tens(string digit)
    {
        int d = Convert.ToInt32(digit);
        return d switch
        {
            10 => "Ten",       11 => "Eleven",    12 => "Twelve",
            13 => "Thirteen",  14 => "Fourteen",  15 => "Fifteen",
            16 => "Sixteen",   17 => "Seventeen", 18 => "Eighteen",
            19 => "Nineteen",  20 => "Twenty",    30 => "Thirty",
            40 => "Forty",     50 => "Fifty",     60 => "Sixty",
            70 => "Seventy",   80 => "Eighty",    90 => "Ninety",
            _ when d > 0 => Tens(d.ToString()[..1] + "0") + " " + Ones(d.ToString()[1..]),
            _ => ""
        };
    }
}
