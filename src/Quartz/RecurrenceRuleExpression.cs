#region License
/* 
 * All content copyright Terracotta, Inc., unless otherwise indicated. All rights reserved. 
 * 
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not 
 * use this file except in compliance with the License. You may obtain a copy 
 * of the License at 
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0 
 *   
 * Unless required by applicable law or agreed to in writing, software 
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT 
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the 
 * License for the specific language governing permissions and limitations 
 * under the License.
 * 
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

using Quartz.Collection;
using Quartz.Util;

namespace Quartz
{
    /// <summary>
    /// Provides a parser and evaluator for unix-like cron expressions. Cron 
    /// expressions provide the ability to specify complex time combinations such as
    /// &quot;At 8:00am every Monday through Friday&quot; or &quot;At 1:30am every 
    /// last Friday of the month&quot;. 
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cron expressions are comprised of 6 required fields and one optional field
    /// separated by white space. The fields respectively are described as follows:
    /// </para>
    /// <table cellspacing="8">
    /// <tr>
    /// <th align="left">Field Name</th>
    /// <th align="left"> </th>
    /// <th align="left">Allowed Values</th>
    /// <th align="left"> </th>
    /// <th align="left">Allowed Special Characters</th>
    /// </tr>
    /// <tr>
    /// <td align="left">Seconds</td>
    /// <td align="left"> </td>
    /// <td align="left">0-59</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// /</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Minutes</td>
    /// <td align="left"> </td>
    /// <td align="left">0-59</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// /</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Hours</td>
    /// <td align="left"> </td>
    /// <td align="left">0-23</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// /</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Day-of-month</td>
    /// <td align="left"> </td>
    /// <td align="left">1-31</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// ? / L W C</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Month</td>
    /// <td align="left"> </td>
    /// <td align="left">1-12 or JAN-DEC</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// /</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Day-of-Week</td>
    /// <td align="left"> </td>
    /// <td align="left">1-7 or SUN-SAT</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// ? / L #</td>
    /// </tr>
    /// <tr>
    /// <td align="left">Year (Optional)</td>
    /// <td align="left"> </td>
    /// <td align="left">empty, 1970-2199</td>
    /// <td align="left"> </td>
    /// <td align="left">, - /// /</td>
    /// </tr>
    /// </table>
    /// <para>
    /// The '*' character is used to specify all values. For example, &quot;*&quot; 
    /// in the minute field means &quot;every minute&quot;.
    /// </para>
    /// <para>
    /// The '?' character is allowed for the day-of-month and day-of-week fields. It
    /// is used to specify 'no specific value'. This is useful when you need to
    /// specify something in one of the two fields, but not the other.
    /// </para>
    /// <para>
    /// The '-' character is used to specify ranges For example &quot;10-12&quot; in
    /// the hour field means &quot;the hours 10, 11 and 12&quot;.
    /// </para>
    /// <para>
    /// The ',' character is used to specify additional values. For example
    /// &quot;MON,WED,FRI&quot; in the day-of-week field means &quot;the days Monday,
    /// Wednesday, and Friday&quot;.
    /// </para>
    /// <para>
    /// The '/' character is used to specify increments. For example &quot;0/15&quot;
    /// in the seconds field means &quot;the seconds 0, 15, 30, and 45&quot;. And 
    /// &quot;5/15&quot; in the seconds field means &quot;the seconds 5, 20, 35, and
    /// 50&quot;.  Specifying '*' before the  '/' is equivalent to specifying 0 is
    /// the value to start with. Essentially, for each field in the expression, there
    /// is a set of numbers that can be turned on or off. For seconds and minutes, 
    /// the numbers range from 0 to 59. For hours 0 to 23, for days of the month 0 to
    /// 31, and for months 1 to 12. The &quot;/&quot; character simply helps you turn
    /// on every &quot;nth&quot; value in the given set. Thus &quot;7/6&quot; in the
    /// month field only turns on month &quot;7&quot;, it does NOT mean every 6th 
    /// month, please note that subtlety.  
    /// </para>
    /// <para>
    /// The 'L' character is allowed for the day-of-month and day-of-week fields.
    /// This character is short-hand for &quot;last&quot;, but it has different 
    /// meaning in each of the two fields. For example, the value &quot;L&quot; in 
    /// the day-of-month field means &quot;the last day of the month&quot; - day 31 
    /// for January, day 28 for February on non-leap years. If used in the 
    /// day-of-week field by itself, it simply means &quot;7&quot; or 
    /// &quot;SAT&quot;. But if used in the day-of-week field after another value, it
    /// means &quot;the last xxx day of the month&quot; - for example &quot;6L&quot;
    /// means &quot;the last friday of the month&quot;. You can also specify an offset 
    /// from the last day of the month, such as "L-3" which would mean the third-to-last 
    /// day of the calendar month. <i>When using the 'L' option, it is important not to 
    /// specify lists, or ranges of values, as you'll get confusing/unexpected results.</i>
    /// </para>
    /// <para>
    /// The 'W' character is allowed for the day-of-month field.  This character 
    /// is used to specify the weekday (Monday-Friday) nearest the given day.  As an 
    /// example, if you were to specify &quot;15W&quot; as the value for the 
    /// day-of-month field, the meaning is: &quot;the nearest weekday to the 15th of
    /// the month&quot;. So if the 15th is a Saturday, the trigger will fire on 
    /// Friday the 14th. If the 15th is a Sunday, the trigger will fire on Monday the
    /// 16th. If the 15th is a Tuesday, then it will fire on Tuesday the 15th. 
    /// However if you specify &quot;1W&quot; as the value for day-of-month, and the
    /// 1st is a Saturday, the trigger will fire on Monday the 3rd, as it will not 
    /// 'jump' over the boundary of a month's days.  The 'W' character can only be 
    /// specified when the day-of-month is a single day, not a range or list of days.
    /// </para>
    /// <para>
    /// The 'L' and 'W' characters can also be combined for the day-of-month 
    /// expression to yield 'LW', which translates to &quot;last weekday of the 
    /// month&quot;.
    /// </para>
    /// <para>
    /// The '#' character is allowed for the day-of-week field. This character is
    /// used to specify &quot;the nth&quot; XXX day of the month. For example, the 
    /// value of &quot;6#3&quot; in the day-of-week field means the third Friday of 
    /// the month (day 6 = Friday and &quot;#3&quot; = the 3rd one in the month). 
    /// Other examples: &quot;2#1&quot; = the first Monday of the month and 
    /// &quot;4#5&quot; = the fifth Wednesday of the month. Note that if you specify
    /// &quot;#5&quot; and there is not 5 of the given day-of-week in the month, then
    /// no firing will occur that month. If the '#' character is used, there can
    /// only be one expression in the day-of-week field (&quot;3#1,6#3&quot; is 
    /// not valid, since there are two expressions).
    /// </para>
    /// <para>
    /// <!--The 'C' character is allowed for the day-of-month and day-of-week fields.
    /// This character is short-hand for "calendar". This means values are
    /// calculated against the associated calendar, if any. If no calendar is
    /// associated, then it is equivalent to having an all-inclusive calendar. A
    /// value of "5C" in the day-of-month field means "the first day included by the
    /// calendar on or after the 5th". A value of "1C" in the day-of-week field
    /// means "the first day included by the calendar on or after Sunday". -->
    /// </para>
    /// <para>
    /// The legal characters and the names of months and days of the week are not
    /// case sensitive.
    /// </para>
    /// <para>
    /// <b>NOTES:</b>
    /// <ul>
    /// <li>Support for specifying both a day-of-week and a day-of-month value is
    /// not complete (you'll need to use the '?' character in one of these fields).
    /// </li>
    /// <li>Overflowing ranges is supported - that is, having a larger number on 
    /// the left hand side than the right. You might do 22-2 to catch 10 o'clock 
    /// at night until 2 o'clock in the morning, or you might have NOV-FEB. It is 
    /// very important to note that overuse of overflowing ranges creates ranges 
    /// that don't make sense and no effort has been made to determine which 
    /// interpretation CronExpression chooses. An example would be 
    /// "0 0 14-6 ? * FRI-MON". </li>
    /// </ul>
    /// </para>
    /// </remarks>
    /// <author>Sharada Jambula</author>
    /// <author>James House</author>
    /// <author>Contributions from Mads Henderson</author>
    /// <author>Refactoring from CronTrigger to CronExpression by Aaron Craven</author>
    /// <author>Marko Lahma (.NET)</author>
    [Serializable]
    public class RecurrenceRuleExpression : ICloneable, IDeserializationCallback
    {

        private TimeZoneInfo timeZone;

        private readonly string recurrenceRuleExpressionString;

        public static readonly int MaxYear = DateTime.Now.Year + 100;

        ///<summary>
        /// Constructs a new <see cref="RecurrenceRuleExpression" /> based on the specified 
        /// parameter.
        /// </summary>
        /// <param name="recurrenceRuleExpression">
        /// String representation of the RRule expression in the new object should represent
        /// </param>
        /// <see cref="RecurrenceRuleExpressionString" />
        public RecurrenceRuleExpression(string recurrenceRuleExpression)
        {
            if (recurrenceRuleExpression == null)
            {
                throw new ArgumentException("recurrenceRuleExpression cannot be null");
            }

            recurrenceRuleExpressionString = recurrenceRuleExpression.ToUpper(CultureInfo.InvariantCulture);
            //TODO: Build Expression(recurrenceRuleExpression);
        }

        /// <summary>
        /// Indicates whether the given date satisfies the cron expression. 
        /// </summary>
        /// <remarks>
        /// Note that  milliseconds are ignored, so two Dates falling on different milliseconds
        /// of the same second will always have the same result here.
        /// </remarks>
        /// <param name="dateUtc">The date to evaluate.</param>
        /// <returns>a boolean indicating whether the given date satisfies the cron expression</returns>
        public virtual bool IsSatisfiedBy(DateTimeOffset dateUtc)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the next date/time <i>after</i> the given date/time which
        /// satisfies the cron expression.
        /// </summary>
        /// <param name="date">the date/time at which to begin the search for the next valid date/time</param>
        /// <returns>the next valid date/time</returns>
        public virtual DateTimeOffset? GetNextValidTimeAfter(DateTimeOffset date)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the next date/time <i>after</i> the given date/time which does
        /// <i>not</i> satisfy the expression.
        /// </summary>
        /// <param name="date">the date/time at which to begin the search for the next invalid date/time</param>
        /// <returns>the next valid date/time</returns>
        public virtual DateTimeOffset? GetNextInvalidTimeAfter(DateTimeOffset date)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Sets or gets the time zone for which the <see cref="Quartz.CronExpression" /> of this
        /// <see cref="ICronTrigger" /> will be resolved.
        /// </summary>
        public virtual TimeZoneInfo TimeZone
        {
            set { timeZone = value; }
            get
            {
                if (timeZone == null)
                {
                    timeZone = TimeZoneInfo.Local;
                }

                return timeZone;
            }
        }

        /// <summary>
        /// Returns the string representation of the <see cref="Quartz.CronExpression" />
        /// </summary>
        /// <returns>The string representation of the <see cref="Quartz.CronExpression" /></returns>
        public override string ToString()
        {
            return recurrenceRuleExpressionString;
        }

        /// <summary>
        /// Indicates whether the specified cron expression can be parsed into a 
        /// valid cron expression
        /// </summary>
        /// <param name="recurrenceRuleExpression">the expression to evaluate</param>
        /// <returns>a boolean indicating whether the given expression is a valid cron
        ///         expression</returns>
        public static bool IsValidExpression(string recurrenceRuleExpression)
        {
            try
            {
                new RecurrenceRuleExpression(recurrenceRuleExpression);
            }
            catch (FormatException)
            {
                return false;
            }

            return true;
        }

        public static void ValidateExpression(string cronExpression)
        {
            new RecurrenceRuleExpression(cronExpression);
        }

        /// <summary>
        /// Gets the cron expression string.
        /// </summary>
        /// <value>The cron expression string.</value>
        public string RecurrenceRuleExpressionString
        {
            get { return recurrenceRuleExpressionString; }
        }

        /// <summary>
        /// Gets the expression summary.
        /// </summary>
        /// <returns></returns>
        public virtual string GetExpressionSummary()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the time before.
        /// </summary>
        /// <param name="endTime">The end time.</param>
        /// <returns></returns>
        public virtual DateTimeOffset? GetTimeBefore(DateTimeOffset? endTime)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the final time that the 
        /// <see cref="Quartz.CronExpression" /> will match.
        /// </summary>
        /// <returns></returns>
        public virtual DateTimeOffset? GetFinalFireTime()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new object that is a copy of the current instance.
        /// </summary>
        /// <returns>
        /// A new object that is a copy of this instance.
        /// </returns>
        public object Clone()
        {
            throw new NotImplementedException();
        }

        public void OnDeserialization(object sender)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines whether the specified <see cref="Quartz.CronExpression"/> is equal to the current <see cref="Quartz.CronExpression"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="Quartz.CronExpression"/> is equal to the current <see cref="Quartz.CronExpression"/>; otherwise, false.
        /// </returns>
        /// <param name="other">The <see cref="Quartz.CronExpression"/> to compare with the current <see cref="Quartz.CronExpression"/>. </param>
        public bool Equals(RecurrenceRuleExpression other)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Determines whether the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>.
        /// </summary>
        /// <returns>
        /// true if the specified <see cref="T:System.Object"/> is equal to the current <see cref="T:System.Object"/>; otherwise, false.
        /// </returns>
        /// <param name="obj">The <see cref="T:System.Object"/> to compare with the current <see cref="T:System.Object"/>. </param>
        public override bool Equals(object obj)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Serves as a hash function for a particular type. 
        /// </summary>
        /// <returns>
        /// A hash code for the current <see cref="T:System.Object"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            throw new NotImplementedException();
        }
    }
}
